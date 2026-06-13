# CLI Plugin Marketplace with Security Analysis and Safe Zone

## Why

Current plugin distribution lacks centralized vetting. Malicious plugins risk developer environments. Organizations need isolated access control. Authors need fair appeal process. This system provides automated security analysis, org-level safe zones, and admin oversight for secure plugin distribution at scale.

## What Changes

**New Security Infrastructure**
- Security Analysis Service: static + dynamic analysis pipeline with scoring
- Safe Zone: org-isolated plugin access with approval workflows
- Control Center: admin dashboard for oversight, metrics, appeals
- Reputation System: karma, badges, leaderboard for plugin authors

**Modified Capabilities**
- Plugin submission now includes mandatory security analysis
- Plugin listing filtered by safe zone membership
- Admin can override analysis decisions via appeal process
- All plugins scored and badged based on analysis results

## Capabilities

### New Capabilities
- `security-analysis` — Static (ESLint, Semgrep, Gitleaks, Trivy) + dynamic (Docker sandbox) analysis on submission
- `scoring-algorithm` — Weighted scoring (static: 60%, dynamic: 40%) with pass/fail thresholds
- `safe-zone` — Org-specific plugin access with isolation and approval workflows
- `control-center` — Admin dashboard for monitoring, metrics, appeal resolution
- `reputation-system` — Karma points, badges (Security Champion, Trusted Author), leaderboard
- `appeal-process` — Authors can appeal false positives with evidence
- `sandbox-execution` — Docker-based (default) / Firecracker (future) for dynamic analysis
- `rate-limiting` — Per-IP and per-author submission limits

### Modified Capabilities
- `plugin-upload` — Now triggers security analysis pipeline before acceptance
- `plugin-catalog` — Shows security badges and scores in listings
- `plugin-search` — Can filter by security score range

## Impact

**New Components**
- Analysis Service (Node.js/Python with Docker sandbox)
- PostgreSQL extensions (analysis_results, appeals, org_safe_zones, reputation tables)
- Control Center UI (Angular dashboard)
- Async queue (PG-based) for analysis jobs

**No Breaking Changes**
- Existing plugin catalog remains functional
- Security analysis is additive layer

**Privacy & Security**
- All analysis runs in isolated containers
- No plugin code executed on marketplace servers (sandbox only)
- Org data isolated by design

## Success Metrics
- Plugin adoption rate (weekly active installs)
- False positive rate (<5% of rejected plugins)
- Appeal resolution time (median <24 hours)
- Safe zone isolation violations (target: 0)
- Author satisfaction score (survey, target: >4.5/5)

## Stakeholders
- **Super-admin (You)**: System configuration, final appeal authority
- **Org Admins**: Safe zone management, plugin approval for their org
- **Plugin Authors**: Submit plugins, view analysis results, file appeals
- **End Users**: Browse safe zone plugins, view security badges

## Scope

### In Scope
- Full security analysis pipeline (static + dynamic)
- Scoring algorithm with configurable thresholds
- Safe zone with org isolation
- Control center dashboard
- Appeal process with evidence submission
- Reputation system (karma, badges, leaderboard)
- Rate limiting and abuse prevention

### Out of Scope
- Plugin execution on marketplace servers (only in sandbox)
- User-side sandboxing (CLI responsibility)
- Real-time analysis (batch processing only)
- Blockchain-based reputation (future consideration)
