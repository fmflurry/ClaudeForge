# Sandbox Security Documentation

## Architecture

The dynamic analysis sandbox executes plugin code in an isolated Docker container to detect runtime behavior without risk to the host system or marketplace infrastructure.

```
┌──────────────────────────────────────────────────────────┐
│                    Analysis Worker Host                      │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │              Docker Container (sandbox)                 │  │
│  │                                                         │  │
│  │  ┌──────────┐  ┌──────────┐  ┌────────────────────┐  │  │
│  │  │ Plugin   │  │ Monitor  │  │ stdout/stderr        │  │  │
│  │  │ Code     │──│ (behav-  │──│ Captured & Scored   │  │  │
│  │  │ Executed │  │ ior)     │  │                      │  │  │
│  │  └──────────┘  └──────────┘  └────────────────────┘  │  │
│  │                                                         │  │
│  │  Read-Only Root Filesystem                              │  │
│  │  No Network Access                                      │  │
│  │  Limited CPU/Memory                                     │  │
│  │  Seccomp + no-new-privileges                            │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                              │
│  Container destroyed after analysis (no persistence)         │
└──────────────────────────────────────────────────────────┘
```

---

## Security Isolation Measures

### 1. No Network Access

Containers run with **network disabled** (`--network none`). Plugins cannot:

- Connect to external servers
- Exfiltrate data
- Download additional payloads
- Call home to C2 infrastructure

If the plugin attempts network access, the behavior is logged as a finding and the dynamic score is reduced.

### 2. Read-Only Root Filesystem

The container root filesystem is mounted **read-only** (`--read-only`). Plugins cannot:

- Modify system binaries
- Write to `/etc`, `/usr`, `/bin`
- Install packages
- Persist changes

A temporary writable directory is provided at `/tmp` with restrictions:

- Mounted as `tmpfs`
- Flags: `rw,noexec,nosuid` (cannot execute binaries, cannot setuid)

### 3. Limited CPU and Memory

Resource limits prevent DoS attacks and resource exhaustion:

| Resource | Limit |
|----------|-------|
| Memory | 512 MB (`--memory=512m`) |
| CPU shares | 512 (low priority relative to host) |
| Processes | 100 max (`--ulimit nproc=100`) |
| Open files | 100 soft / 200 hard (`--ulimit nofile=100:200`) |

### 4. Seccomp Profile

Default Docker seccomp profile blocks dangerous syscalls:

- `clone` with certain flags restricted
- `mount` / `umount` blocked
- `ptrace` blocked
- `process_vm_writev` blocked
- `reboot` / `swapon` blocked

Custom seccomp policy can be supplied for tighter restrictions.

### 5. No New Privileges

`--security-opt=no-new-privileges:true` prevents:

- `setuid` / `setgid` binary execution
- Capability escalation
- Privilege escalation via `su` / `sudo`

### 6. Dropped Capabilities

All Linux capabilities are dropped by default, with only minimal additions:

| Action | Capabilities |
|--------|-------------|
| Dropped | `ALL` |
| Added back | `CHOWN`, `SETGID`, `SETUID` (required for basic plugin operation) |

This means the container cannot:

- Perform raw network operations (`NET_RAW`, `NET_ADMIN`)
- Load kernel modules (`SYS_MODULE`)
- Change system time (`SYS_TIME`)
- Access hardware (`SYS_RAWIO`)

---

## Timeout

| Parameter | Value |
|-----------|-------|
| Dynamic analysis timeout | **3 minutes (180 seconds)** |
| Action on timeout | Container killed, timeout finding recorded, -50 points |
| Total analysis limit | 5 minutes per plugin (static 2m + dynamic 3m) |

If the plugin exceeds 3 minutes of execution time, it is terminated immediately. The partial output and behavior log are still scored (with a deduction).

---

## Filesystem Details

| Path | Access | Purpose |
|------|--------|---------|
| `/` | Read-only | System files, libraries |
| `/tmp` | Read-write, noexec, nosuid | Plugin workspace, temp files |
| `/plugin` | Read-only | Plugin code mounted from host |
| `/output` | Read-write | Analysis output captured from stdout |

Plugin source code is mounted from the host at `/plugin` as read-only. The plugin cannot modify its own source during analysis.

Output is captured via:

- **stdout**: Structured JSON output from plugin test runs
- **stderr**: Error messages and diagnostics
- **Behavior monitor**: System call tracing for suspicious behavior

---

## How Plugins Are Executed Safely

### Execution Flow

1. **Extract**: Plugin package is extracted to a temporary directory on the host
2. **Mount**: Plugin directory is bind-mounted into container at `/plugin` (read-only)
3. **Initialize**: Container starts with minimal config (no network, limited resources)
4. **Execute**: Plugin runs with various test inputs for up to 3 minutes
5. **Monitor**: Behavior monitor (strace-based) captures syscalls
6. **Capture**: stdout/stderr output is collected
7. **Terminate**: Container is destroyed (no persistence)
8. **Score**: Behavior findings and output are scored

### Docker Run Command

```bash
docker run \
  --rm \
  --network none \
  --read-only \
  --memory=512m \
  --cpu-shares=512 \
  --ulimit nproc=100 \
  --ulimit nofile=100:200 \
  --cap-drop=ALL \
  --cap-add=CHOWN,SETGID,SETUID \
  --security-opt=no-new-privileges:true \
  --tmpfs /tmp:rw,noexec,nosuid \
  -v /host/plugin/path:/plugin:ro \
  -v /host/output:/output:rw \
  alpine:latest \
  /plugin/entrypoint.sh
```

---

## Docker Compose Configuration

```yaml
version: '3.8'
services:
  sandbox:
    image: alpine:latest
    read_only: true
    tmpfs:
      - /tmp:rw,noexec,nosuid
    cap_drop:
      - ALL
    cap_add:
      - CHOWN
      - SETGID
      - SETUID
    security_opt:
      - no-new-privileges:true
    mem_limit: 512m
    cpu_shares: 512
    ulimits:
      nproc: 100
      nofile:
        soft: 100
        hard: 200
    network_mode: none
    volumes:
      - /host/plugin:/plugin:ro
      - /host/output:/output:rw
```

---

## Attack Surface Analysis

### Potential Attack Vectors

| Vector | Risk | Mitigation |
|--------|------|------------|
| **Container escape** via kernel exploit | Low | Seccomp + dropped caps + no-new-privileges + read-only rootfs |
| **Resource exhaustion** (fork bomb) | Low | `nproc=100` limit prevents runaway processes |
| **Memory exhaustion** (OOM) | Low | `memory=512m` enforced by cgroups |
| **Filesystem escape** (symlink attacks) | Low | Read-only rootfs + non-root user inside container |
| **Network exfiltration** | None | `--network none` — no network stack available |
| **Cryptomining** | None | No network, limited CPU/memory, short timeout |
| **Data persistence** (malicious code persists across runs) | None | Container is ephemeral — destroyed after each analysis |
| **Privilege escalation** | Low | `no-new-privileges` + all caps dropped |

### Threat Model

- **Attacker goal**: Execute malicious code on analysis infrastructure, exfiltrate data, or disrupt service
- **Attacker capability**: Can submit arbitrary plugin code with full knowledge of sandbox restrictions
- **Trust boundary**: Between plugin code (untrusted) and analysis worker (trusted)
- **Assumptions**: Docker daemon is secure, kernel CVEs are patched, analysis worker has no secrets in reach

---

## Known Limitations

| Limitation | Impact | Mitigation |
|------------|--------|------------|
| **Not a full OS sandbox** | Sophisticated kernel exploits may still escape | Regular kernel updates, seccomp profile hardening, consider Firecracker for higher isolation |
| **Side-channel attacks** | Covert timing/inference channels within same host | Isolate analysis workers to dedicated hosts, avoid co-tenancy with sensitive workloads |
| **Docker daemon attack surface** | Docker socket is exposed to analysis workers | Run workers with minimal Docker socket access, use TLS, consider rootless Docker |
| **No filesystem encryption** | Plugin code on host disk is unencrypted | Encrypt analysis worker filesystems, purge temp directories after completion |
| **Traffic analysis** | Plugin can infer timing patterns from its own execution | Acceptable for MVP; side-channel resistance is future work |
| **Firecracker not yet available** | Docker provides weaker isolation than micro-VMs | Firecracker integration is Phase 2 if Docker isolation proves insufficient |

---

## Future Improvements

1. **Firecracker micro-VMs**: Hardware-level isolation via KVM-based micro-VMs
2. **gVisor**: Additional syscall filtering for defense-in-depth
3. **Rootless Docker**: Eliminate container-root attack surface
4. **Seccomp profile customization**: Per-plugin-type seccomp policies
5. **Runtime anomaly detection**: ML-based behavioral anomaly detection
6. **Audit logging for sandbox events**: Detailed container lifecycle logging
