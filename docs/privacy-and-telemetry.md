<!-- slug: privacy-and-telemetry | category: reference -->

# Privacy & Telemetry

ClaudeForge collects anonymized telemetry to understand plugin adoption and improve the marketplace. This page explains exactly what data we collect, how we use it, and how to opt out.

## What We Collect

ClaudeForge collects **anonymized events only**. We do not collect personally identifiable information (PII).

### Event Data

Each telemetry event includes:

| Field | Example | Notes |
|-------|---------|-------|
| `eventType` | "install", "download" | Type of action taken |
| `pluginId` | "550e8400-e29b-41d4-a716-446655440000" | UUID of the plugin |
| `version` | "1.2.3" | Plugin version (if applicable) |
| `anonClientId` | "sha256:a1b2c3..." | Hashed anonymous identifier (not linked to identity) |
| `clientOs` | "darwin", "linux", "win32" | Operating system (coarse-grained) |
| `clientArch` | "x86_64", "arm64" | CPU architecture (coarse-grained) |

### What We DON'T Collect

- IP addresses
- Usernames or email addresses
- System hostnames or domain names
- File paths or project names
- Plugin configuration or settings
- Specific usage patterns or workflows
- Personal device details beyond OS/arch
- Timestamps (events are aggregated without time)

## How We Use This Data

### Analytics & Insights

We use anonymized telemetry to:
- Track plugin popularity and adoption trends
- Identify which plugin types and languages are most used
- Understand platform growth metrics
- Detect issues with plugin distribution
- Plan roadmap priorities based on usage

### Aggregate Reports

Data is aggregated and presented as:
- Total downloads/installs by plugin
- Top plugins by category
- Platform usage distribution (OS, architecture)
- Monthly growth metrics

Individual events are **never** tied to specific users or systems.

### No Third-Party Sharing

We do not:
- Share raw telemetry with third parties
- Sell or license your event data
- Provide data to advertisers
- Pass data to analytics platforms without aggregation

## Data Retention

### Raw Events

Raw telemetry events are retained for **90 days** from collection:
- Allows us to detect and fix real-time issues
- Enables quick trend analysis
- Supports privacy by auto-deleting after the window

### Aggregated Data

After 90 days, raw events are deleted and replaced with aggregate statistics:
- Total download/install counts
- Platform distribution percentages
- Trend summaries

Aggregate data is retained indefinitely (it contains no identifying information).

## Opting Out

Telemetry is **enabled by default** but can be disabled at any time.

### Via CLI

Disable telemetry globally:

```bash
claude-plugin config set telemetry enabled=false
```

Re-enable it later:

```bash
claude-plugin config set telemetry enabled=true
```

Check current status:

```bash
claude-plugin config get telemetry
```

### Via Web UI

In the ClaudeForge web dashboard:
1. Navigate to **Settings**
2. Find **Telemetry & Privacy**
3. Toggle **Send usage data** off/on

Changes take effect immediately.

### What Happens When You Opt Out

When telemetry is disabled:
- No events are sent to the marketplace
- Your plugin downloads/installs are still counted (by the marketplace, not by your client)
- You receive no tracking cookies or identifiers
- Your privacy is fully respected

**Note:** Opting out does not hide your plugin from search or prevent others from discovering it.

## How We Protect Your Data

### Anonymization

All telemetry is anonymized before storage:
- `anonClientId` is a **salted hash**, not a username or email
- OS and architecture are **coarse-grained** (not device-specific)
- No timestamps are stored with individual events
- No geographic data is collected

### Storage & Access

- Telemetry data is stored in a PostgreSQL database
- Access is restricted to ClaudeForge engineering team
- Data is encrypted in transit (HTTPS)
- No external analytics platforms (Google Analytics, Mixpanel, etc.) have access

### Future Encryption

We are planning:
- End-to-end encryption for telemetry (opt-in beta)
- Differential privacy techniques for additional anonymization
- Zero-knowledge telemetry aggregation

## Compliance

ClaudeForge aims to respect privacy regulations:
- **GDPR**: No personal data is collected, so GDPR's definition of personal data does not apply
- **CCPA**: Users have the right to opt out (via telemetry disable)
- **PIPEDA**: No PII is stored or shared

## Questions?

If you have concerns about privacy or telemetry:
- Review this page and the [FAQ](./faq.md)
- Check your config: `claude-plugin config get`
- Open an issue on the [GitHub repository](https://github.com/anthropics/ClaudeForge)

## Changes to This Policy

We may update this policy as ClaudeForge evolves. Changes will be:
- Posted on this page with a version date
- Announced in release notes
- Effective 30 days after posting (unless legally required sooner)

Last updated: June 2026
