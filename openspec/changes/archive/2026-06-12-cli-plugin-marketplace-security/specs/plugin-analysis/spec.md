# Plugin Analysis Specification

## ADDED Requirements

### Requirement: Perform static analysis on all plugin submissions

The system SHALL run static analysis using multiple tools on every plugin submission.

#### Scenario: Run static analysis with all configured tools
**WHEN** a plugin is submitted for analysis

**THEN** the system SHALL:
- Extract plugin package to temporary directory
- Run ESLint on all JavaScript/TypeScript files
- Run Semgrep with security rules on all files
- Run Gitleaks to detect secrets
- Run Trivy to scan dependencies
- Aggregate results from all tools

#### Scenario: Static analysis finds security issues
**WHEN** ESLint finds 5 warnings, Semgrep finds 2 security issues, Gitleaks finds 1 secret, Trivy finds 3 vulnerabilities

**THEN** the system SHALL:
- Record all findings with severity levels
- Calculate static score based on findings
- Include all findings in analysis results

#### Scenario: Static analysis finds no issues
**WHEN** all static analysis tools complete with no findings

**THEN** the system SHALL:
- Record perfect static score (100)
- Mark static analysis as passed

### Requirement: Perform dynamic analysis in sandbox

The system SHALL execute plugins in isolated sandbox to detect runtime behavior.

#### Scenario: Run plugin in Docker sandbox
**WHEN** static analysis completes

**THEN** the system SHALL:
- Start Docker container with plugin mounted
- Execute plugin with test inputs
- Monitor plugin behavior (file access, network, process spawning)
- Capture all output and errors
- Destroy container after analysis

#### Scenario: Dynamic analysis detects suspicious behavior
**WHEN** plugin attempts to access network during execution

**THEN** the system SHALL:
- Record network access attempt as finding
- Reduce dynamic score based on severity
- Continue monitoring until timeout

#### Scenario: Dynamic analysis times out
**WHEN** plugin execution exceeds 3 minute timeout

**THEN** the system SHALL:
- Terminate container immediately
- Record timeout as finding
- Assign minimum score for dynamic analysis

### Requirement: Calculate weighted score from analysis results

The system SHALL calculate a weighted score combining static and dynamic analysis.

#### Scenario: Calculate score with default weights
**GIVEN** static score = 90, dynamic score = 70
**AND** static weight = 0.6, dynamic weight = 0.4

**THEN** the system SHALL calculate total score as:
```
total_score = (90 * 0.6) + (70 * 0.4) = 54 + 28 = 82
```

#### Scenario: Calculate score with custom weights
**GIVEN** static score = 85, dynamic score = 65
**AND** static weight = 0.7, dynamic weight = 0.3

**THEN** the system SHALL calculate total score as:
```
total_score = (85 * 0.7) + (65 * 0.3) = 59.5 + 19.5 = 79
```

### Requirement: Apply thresholds to determine pass/fail

The system SHALL use configurable thresholds to determine analysis outcome.

#### Scenario: Plugin passes with score above threshold
**GIVEN** total score = 85
**AND** pass threshold = 80

**THEN** the system SHALL mark analysis result as `passed`

#### Scenario: Plugin fails with score below threshold
**GIVEN** total score = 45
**AND** fail threshold = 50

**THEN** the system SHALL mark analysis result as `failed`

#### Scenario: Plugin requires review with score in middle range
**GIVEN** total score = 65
**AND** pass threshold = 80
**AND** fail threshold = 50

**THEN** the system SHALL mark analysis result as `in_review`

### Requirement: Store detailed analysis results

The system SHALL store complete analysis results for every submission.

#### Scenario: Store static analysis findings
**WHEN** static analysis completes

**THEN** the system SHALL store for each finding:
- Tool name (eslint, semgrep, gitleaks, trivy)
- Severity level (low, medium, high, critical)
- Message/description
- File path
- Line number (if available)
- Column number (if available)

#### Scenario: Store dynamic analysis findings
**WHEN** dynamic analysis completes

**THEN** the system SHALL store for each finding:
- Type (file_access, network_access, process_spawn, timeout, error)
- Severity level
- Description
- Timestamp
- Context (file path, command, etc.)

### Requirement: Support configurable analysis parameters

The system SHALL allow configuration of analysis parameters.

#### Scenario: Configure static analysis weights
**GIVEN** configuration:
```yaml
static:
  eslint_weight: 0.3
  semgrep_weight: 0.3
  gitleaks_weight: 0.2
  trivy_weight: 0.2
```

**WHEN** calculating static score from individual tool scores

**THEN** the system SHALL use configured weights

#### Scenario: Configure pass/fail thresholds
**GIVEN** configuration:
```yaml
pass_threshold: 85
fail_threshold: 45
```

**WHEN** determining analysis outcome

**THEN** the system SHALL use configured thresholds

---

## Analysis Tools Configuration

### ESLint
**Purpose**: JavaScript/TypeScript code quality and security linting

**Configuration**:
```json
{
  "extends": ["eslint:recommended", "plugin:@typescript-eslint/recommended"],
  "rules": {
    "no-console": "error",
    "no-unused-vars": "warn",
    "@typescript-eslint/no-explicit-any": "error"
  }
}
```

**Scoring**:
- Error: -5 points each
- Warning: -2 points each
- Max deduction: 50 points

### Semgrep
**Purpose**: Multi-language static analysis with security rules

**Rulesets**:
- `p/security-audit` (security-focused)
- `p/secrets` (secret detection)
- Custom rules for plugin-specific patterns

**Scoring**:
- Critical: -10 points each
- High: -7 points each
- Medium: -4 points each
- Low: -2 points each
- Max deduction: 50 points

### Gitleaks
**Purpose**: Secret detection (API keys, tokens, passwords)

**Configuration**:
- Use default rules plus custom patterns
- Scan all files, not just code

**Scoring**:
- Any secret found: -20 points each (critical)
- Max deduction: 100 points (automatic fail if any secrets found)

### Trivy
**Purpose**: Vulnerability scanning for dependencies

**Configuration**:
- Scan for known CVEs in dependencies
- Ignore dev dependencies (configurable)

**Scoring**:
- Critical vulnerability: -10 points each
- High vulnerability: -7 points each
- Medium vulnerability: -4 points each
- Low vulnerability: -2 points each
- Max deduction: 50 points

---

## Dynamic Analysis Configuration

### Docker Sandbox
**Configuration**:
```yaml
sandbox:
  image: "alpine:latest"
  mem_limit: "512m"
  cpu_shares: 512
  timeout: 180  # 3 minutes
  
  security:
    read_only: true
    cap_drop: ["ALL"]
    cap_add: ["CHOWN", "SETGID", "SETUID"]
    security_opt: ["no-new-privileges:true"]
    
  tmpfs:
    - "/tmp:rw,noexec,nosuid"
    
  ulimits:
    nproc: 100
    nofile:
      soft: 100
      hard: 200
```

**Test Inputs**:
- Empty input
- Sample code snippets
- Various file types
- Edge cases (large files, special characters)

**Monitored Behaviors**:
- File system access (read/write/delete)
- Network access (outbound connections)
- Process spawning (child processes)
- Resource usage (CPU, memory)
- Execution time

**Scoring**:
- Each suspicious behavior: -5 to -20 points based on severity
- Timeout: -50 points
- Crash/error: -30 points
- Max deduction: 100 points

---

## Scoring Algorithm

### Static Score Calculation
```
static_score = 100

// ESLint
static_score -= (eslint_errors * 5) + (eslint_warnings * 2)

// Semgrep
static_score -= (semgrep_critical * 10) + (semgrep_high * 7) + (semgrep_medium * 4) + (semgrep_low * 2)

// Gitleaks
if (gitleaks_findings > 0) {
  static_score = 0  // Automatic fail for secrets
}

// Trivy
static_score -= (trivy_critical * 10) + (trivy_high * 7) + (trivy_medium * 4) + (trivy_low * 2)

// Clamp to 0-100
static_score = Math.max(0, Math.min(100, static_score))
```

### Dynamic Score Calculation
```
dynamic_score = 100

// Behavior findings
dynamic_score -= behavior_findings.reduce((sum, finding) => {
  switch (finding.severity) {
    case 'critical': return sum + 20
    case 'high': return sum + 10
    case 'medium': return sum + 5
    case 'low': return sum + 2
    default: return sum
  }
}, 0)

// Timeout
dynamic_score -= timeout_occurred ? 50 : 0

// Crash
dynamic_score -= crash_occurred ? 30 : 0

// Clamp to 0-100
dynamic_score = Math.max(0, Math.min(100, dynamic_score))
```

### Total Score Calculation
```
total_score = (static_score * static_weight) + (dynamic_score * dynamic_weight)

// Default weights
static_weight = 0.6
dynamic_weight = 0.4
```

---

## Decision Matrix

| Score Range | Status | Action |
|------------|--------|--------|
| ≥ pass_threshold (default 80) | passed | Accept to catalog |
| ≥ fail_threshold (default 50) and < pass_threshold | in_review | Manual review queue |
| < fail_threshold (default 50) | failed | Reject, notify author |

---

## Findings Severity Levels

| Level | Weight | Description |
|-------|--------|-------------|
| critical | Highest | Immediate security risk (secrets, RCE vulnerabilities) |
| high | High | Significant security risk (privilege escalation, data exposure) |
| medium | Medium | Moderate security risk (information disclosure, DoS) |
| low | Low | Minor security risk (best practice violations) |

---

## API Contract

### GET /api/v1/plugins/{pluginId}/analysis
**Response (200 OK)**:
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440001",
  "pluginId": "550e8400-e29b-41d4-a716-446655440000",
  "pluginVersion": "1.0.0",
  "status": "completed",
  "result": "passed",
  "scores": {
    "total": 85.5,
    "static": 90.0,
    "dynamic": 75.0
  },
  "staticAnalysis": {
    "eslint": {
      "score": 95.0,
      "errors": 0,
      "warnings": 2,
      "findings": [...]
    },
    "semgrep": {
      "score": 90.0,
      "findings": [...]
    },
    "gitleaks": {
      "score": 100.0,
      "findings": []
    },
    "trivy": {
      "score": 80.0,
      "findings": [...]
    }
  },
  "dynamicAnalysis": {
    "score": 75.0,
    "findings": [...],
    "behaviors": {
      "fileAccess": [...],
      "networkAccess": [],
      "processSpawn": [...]
    },
    "timeout": false,
    "error": null
  },
  "thresholds": {
    "pass": 80,
    "fail": 50,
    "staticWeight": 0.6,
    "dynamicWeight": 0.4
  },
  "inCatalog": true,
  "canAppeal": false,
  "createdAt": "2024-01-01T00:00:00Z",
  "completedAt": "2024-01-01T00:05:00Z"
}
```

---

## Configuration Options

```yaml
analysis:
  # Score weights
  static_weight: 0.6
  dynamic_weight: 0.4
  
  # Thresholds
  pass_threshold: 80
  fail_threshold: 50
  
  # Static analysis
  static:
    eslint:
      enabled: true
      config: "eslint-config-security"
      weight: 0.25
      
    semgrep:
      enabled: true
      rulesets: ["p/security-audit", "p/secrets"]
      weight: 0.25
      
    gitleaks:
      enabled: true
      config: "gitleaks.toml"
      weight: 0.3
      auto_fail: true  # Fail immediately if any secrets found
      
    trivy:
      enabled: true
      severity: ["CRITICAL", "HIGH", "MEDIUM", "LOW"]
      weight: 0.2
  
  # Dynamic analysis
  dynamic:
    enabled: true
    sandbox: "docker"  # or "firecracker"
    timeout: 180  # seconds
    
    docker:
      image: "alpine:latest"
      mem_limit: "512m"
      cpu_shares: 512
      
    firecracker:
      enabled: false
      mem_limit: "256m"
      vcpu: 1
  
  # Queue
  queue:
    max_concurrent: 100
    retry_attempts: 3
    retry_delay: 60  # seconds
```

---

## Error Handling

| Error | Action |
|-------|--------|
| Tool execution failure | Log error, continue with other tools, reduce score |
| Tool timeout | Kill process, log timeout, continue with other tools |
| Sandbox startup failure | Mark job as failed, retry later |
| Container crash | Record as finding, continue analysis |
| Package extraction failure | Fail analysis, notify author |

---

## Performance Requirements

- **Total analysis time**: ≤ 5 minutes per plugin
- **Static analysis**: ≤ 2 minutes
- **Dynamic analysis**: ≤ 3 minutes
- **Queue processing**: 100 concurrent jobs
- **Memory usage**: ≤ 1GB per analysis job
- **Storage**: Analysis results retained for 90 days
