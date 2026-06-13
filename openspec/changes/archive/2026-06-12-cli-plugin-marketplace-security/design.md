# CLI Plugin Marketplace Security Design

## Context

**Current State**: Existing plugin marketplace lacks security vetting. Plugins uploaded directly to catalog without analysis. No org-level access control. No admin oversight for disputes.

**Constraints**
- **Infrastructure**: Extend existing PostgreSQL, add Docker sandbox
- **Technology**: Node.js/Python for analysis, PostgreSQL for queue, Docker for sandbox
- **Security**: No plugin execution on marketplace servers (sandbox only)
- **Performance**: Analysis must complete within 5 minutes per plugin
- **Privacy**: Org data isolated, no cross-org leakage

**Global Rules**
- No `any` types in TypeScript
- Angular components use facades only (never direct use-case access)
- Immutability: all data transformations produce new objects
- Clean Architecture: presentation → application → domain → infrastructure

---

## Goals / Non-Goals

### Goals
1. **Automated Security Analysis**: Every plugin submission analyzed before catalog acceptance
2. **Org Isolation**: Safe zones provide org-specific plugin access
3. **Admin Oversight**: Control center for monitoring and dispute resolution
4. **Author Incentives**: Reputation system rewards quality contributions
5. **False Positive Minimization**: Appeal process with human review

### Non-Goals
- Plugin execution on marketplace servers (sandbox only)
- User-side sandboxing (CLI responsibility)
- Real-time analysis (batch processing acceptable)
- Manual security review for every submission

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              CLI Plugin Marketplace                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────────────────────┐  │
│  │   CLI       │    │   Web UI    │    │        API Gateway           │  │
│  │ (Node.js)   │───▶│ (Angular)   │───▶│ (Rate Limiting, Auth, Routing)│  │
│  └─────────────┘    └─────────────┘    └─────────────┬───────────────┘  │
│                                                   │                    │
│                                                   ▼                    │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                        Analysis Service                            │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌─────────────────────────┐  │  │
│  │  │ Static Analysis│  │ Dynamic       │  │ Scoring &               │  │  │
│  │  │ (ESLint,      │  │ Analysis      │  │ Decision Engine         │  │  │
│  │  │  Semgrep,     │  │ (Docker       │  │ (Weighted algorithm)     │  │  │
│  │  │  Gitleaks,    │  │  Sandbox)     │  │                         │  │  │
│  │  │  Trivy)      │  │              │  │                         │  │  │
│  │  └──────────────┘  └──────────────┘  └─────────────────────────┘  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                   │                    │
│                                                   ▼                    │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                        PostgreSQL                                 │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌─────────────────────────┐  │  │
│  │  │ Plugin        │  │ Analysis      │  │ Safe Zone               │  │  │
│  │  │ Catalog       │  │ Results       │  │ (Org Isolation)         │  │  │
│  │  └──────────────┘  └──────────────┘  └─────────────────────────┘  │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌─────────────────────────┐  │  │
│  │  │ Appeals       │  │ Reputation    │  │ Async Queue (PG-based)  │  │  │
│  │  │               │  │ (Karma, Badges)│  │                         │  │  │
│  │  └──────────────┘  └──────────────┘  └─────────────────────────┘  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                      Control Center Dashboard                       │  │
│  │  (Admin monitoring, metrics, appeal resolution)                    │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Component Breakdown

### 1. CLI (Node.js)
**Responsibilities**:
- Plugin submission (`claude plugin submit <path>`)
- Safe zone listing (`claude plugin list --safe-zone <org>`)
- Analysis results viewing (`claude plugin status <id>`)
- Appeal submission (`claude plugin appeal <id> --reason "false positive"`)

**Key Features**:
- Rate-limited submission (configurable per-author limits)
- Safe zone filtering based on org membership
- Security badge display in listings

### 2. API Gateway
**Responsibilities**:
- Request authentication (API keys for service-to-service)
- Rate limiting (per-IP, per-author)
- Request routing to appropriate services
- Response formatting (RFC 7807 ProblemDetails)

**Endpoints**:
- `POST /api/v1/plugins/submit` - Submit plugin for analysis
- `GET /api/v1/plugins/{id}/analysis` - Get analysis results
- `POST /api/v1/plugins/{id}/appeal` - Submit appeal
- `GET /api/v1/safe-zone/{orgId}/plugins` - List org-safe plugins
- `POST /api/v1/safe-zone/{orgId}/plugins/{pluginId}/approve` - Approve for org
- `GET /api/v1/control-center/metrics` - Admin metrics
- `GET /api/v1/reputation/leaderboard` - Author leaderboard

### 3. Analysis Service
**Responsibilities**:
- Static analysis (ESLint, Semgrep, Gitleaks, Trivy)
- Dynamic analysis (Docker sandbox execution)
- Score calculation (weighted algorithm)
- Decision making (pass/fail based on thresholds)

**Components**:
- **Static Analyzer**: Runs linters and security scanners
- **Dynamic Analyzer**: Executes plugin in Docker sandbox
- **Scoring Engine**: Calculates weighted score (static: 60%, dynamic: 40%)
- **Decision Engine**: Applies thresholds (default: pass ≥ 80, fail < 50, review 50-80)

### 4. PostgreSQL Schema
**Based on m0011 decisions**:

```sql
-- Plugin catalog (existing, extended)
CREATE TABLE plugins (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name TEXT NOT NULL,
  name_normalized TEXT NOT NULL UNIQUE,
  slug TEXT NOT NULL UNIQUE,
  description TEXT NOT NULL,
  author TEXT NOT NULL,
  download_count BIGINT NOT NULL DEFAULT 0,
  security_score DECIMAL(5,2) DEFAULT 0,
  security_status TEXT NOT NULL DEFAULT 'pending' CHECK (security_status IN ('pending', 'passed', 'failed', 'in_review')),
  created_at TIMESTAMPTZ DEFAULT NOW(),
  updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Analysis results
CREATE TABLE analysis_results (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  plugin_id UUID NOT NULL REFERENCES plugins ON DELETE CASCADE,
  plugin_version TEXT NOT NULL,
  -- Static analysis
  static_eslint_score DECIMAL(5,2),
  static_semgrep_score DECIMAL(5,2),
  static_gitleaks_score DECIMAL(5,2),
  static_trivy_score DECIMAL(5,2),
  static_findings JSONB DEFAULT '[]',  -- Array of {severity, message, file, line}
  -- Dynamic analysis
  dynamic_behavior_score DECIMAL(5,2),
  dynamic_findings JSONB DEFAULT '[]',  -- Array of {type, description, severity}
  -- Overall
  total_score DECIMAL(5,2) NOT NULL,
  status TEXT NOT NULL CHECK (status IN ('passed', 'failed', 'in_review')),
  analysis_completed_at TIMESTAMPTZ,
  -- Thresholds
  static_weight DECIMAL(5,2) DEFAULT 0.6,
  dynamic_weight DECIMAL(5,2) DEFAULT 0.4,
  pass_threshold DECIMAL(5,2) DEFAULT 80,
  fail_threshold DECIMAL(5,2) DEFAULT 50
);

-- Safe zones (org isolation)
CREATE TABLE organizations (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name TEXT NOT NULL UNIQUE,
  slug TEXT NOT NULL UNIQUE,
  created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE org_members (
  org_id UUID NOT NULL REFERENCES organizations ON DELETE CASCADE,
  user_id UUID NOT NULL,  -- Author/user identifier
  role TEXT NOT NULL CHECK (role IN ('admin', 'member')),
  joined_at TIMESTAMPTZ DEFAULT NOW(),
  PRIMARY KEY (org_id, user_id)
);

CREATE TABLE safe_zone_plugins (
  org_id UUID NOT NULL REFERENCES organizations ON DELETE CASCADE,
  plugin_id UUID NOT NULL REFERENCES plugins ON DELETE CASCADE,
  plugin_version TEXT NOT NULL,
  approved_by UUID NOT NULL,  -- User who approved
  approved_at TIMESTAMPTZ DEFAULT NOW(),
  is_active BOOLEAN DEFAULT TRUE,
  PRIMARY KEY (org_id, plugin_id, plugin_version)
);

-- Appeals
CREATE TABLE appeals (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  plugin_id UUID NOT NULL REFERENCES plugins ON DELETE CASCADE,
  analysis_result_id UUID REFERENCES analysis_results ON DELETE CASCADE,
  author_id UUID NOT NULL,  -- Plugin author
  reason TEXT NOT NULL,
  evidence TEXT,  -- Author's evidence for false positive
  status TEXT NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'approved', 'rejected')),
  reviewed_by UUID,  -- Admin who reviewed
  reviewed_at TIMESTAMPTZ,
  resolution TEXT,
  created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Reputation system
CREATE TABLE author_reputation (
  author_id UUID NOT NULL PRIMARY KEY,
  karma_points INTEGER DEFAULT 0,
  level INTEGER DEFAULT 1,
  badges JSONB DEFAULT '[]'  -- Array of badge names
);

CREATE TABLE reputation_events (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  author_id UUID NOT NULL REFERENCES author_reputation(author_id) ON DELETE CASCADE,
  event_type TEXT NOT NULL,  -- 'plugin_passed', 'plugin_failed', 'appeal_approved', 'bug_bounty'
  points INTEGER NOT NULL,
  description TEXT,
  created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Async queue (PG-based, no external deps)
CREATE TABLE analysis_jobs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  plugin_id UUID NOT NULL REFERENCES plugins ON DELETE CASCADE,
  plugin_version TEXT NOT NULL,
  status TEXT NOT NULL DEFAULT 'queued' CHECK (status IN ('queued', 'processing', 'completed', 'failed')),
  priority INTEGER DEFAULT 0,
  attempts INTEGER DEFAULT 0,
  last_error TEXT,
  created_at TIMESTAMPTZ DEFAULT NOW(),
  started_at TIMESTAMPTZ,
  completed_at TIMESTAMPTZ
);

-- Indexes
CREATE INDEX idx_analysis_results_plugin ON analysis_results(plugin_id);
CREATE INDEX idx_analysis_results_status ON analysis_results(status);
CREATE INDEX idx_safe_zone_org ON safe_zone_plugins(org_id);
CREATE INDEX idx_safe_zone_plugin ON safe_zone_plugins(plugin_id);
CREATE INDEX idx_appeals_status ON appeals(status);
CREATE INDEX idx_appeals_plugin ON appeals(plugin_id);
CREATE INDEX idx_analysis_jobs_status ON analysis_jobs(status);
CREATE INDEX idx_analysis_jobs_priority ON analysis_jobs(priority DESC, created_at);
```

### 5. Control Center (Admin Dashboard)
**Responsibilities**:
- Monitor analysis pipeline health
- View and resolve appeals
- Configure analysis thresholds
- Manage org safe zones
- View system metrics

**Components**:
- **Dashboard**: Overview of system health, queue status, recent analyses
- **Appeals Queue**: List of pending appeals with quick actions
- **Metrics**: Plugin adoption, false positive rate, appeal resolution time
- **Configuration**: Adjust analysis weights and thresholds
- **Org Management**: View and manage org safe zones

---

## Key Flows

### Flow 1: Plugin Submission → Analysis → Accept/Reject

```
┌─────────┐     ┌─────────────┐     ┌─────────────────┐     ┌─────────────┐
│ Author  │────▶│ Submit Plugin│────▶│ Add to Queue   │────▶│ Analyze     │
│         │     │ (CLI/Web)    │     │ (PG-based)      │     │ (Static+    │
└─────────┘     └─────────────┘     └─────────────────┘     │ Dynamic)    │
                                                          └──────┬──────┘
                                                               │
                    ┌──────────────────────────────────────┼──────────────────────────────────────┐
                    │                                          │                                  │
                    ▼                                          ▼                                  ▼
            ┌──────────────┐                          ┌──────────────┐              ┌──────────────┐
            │ Score ≥ 80    │                          │ Score 50-79  │              │ Score < 50   │
            │ (Pass)       │                          │ (Review)     │              │ (Fail)      │
            └──────┬───────┘                          └──────┬───────┘              └──────┬───────┘
                   │                                         │                                │
                   ▼                                         ▼                                ▼
            ┌──────────────┐                          ┌──────────────┐              ┌──────────────┐
            │ Accept Plugin │                          │ Manual Review│              │ Reject Plugin│
            │ to Catalog   │                          │ Queue        │              │ (Notify      │
            └──────────────┘                          └──────────────┘              │ Author)     │
                                                                                     └──────────────┘
```

**Steps**:
1. Author submits plugin via CLI or Web UI
2. System validates package format and manifest
3. Plugin added to analysis queue (PG-based)
4. Worker picks up job, runs static analysis (ESLint, Semgrep, Gitleaks, Trivy)
5. Worker runs dynamic analysis in Docker sandbox
6. Scoring engine calculates weighted score
7. Decision engine applies thresholds:
   - Pass (≥80): Plugin accepted to catalog
   - Review (50-79): Added to manual review queue
   - Fail (<50): Rejected, author notified
8. Author can appeal any rejection

### Flow 2: Safe Zone Access

```
┌─────────┐     ┌─────────────┐     ┌─────────────────┐     ┌─────────────┐
│ User    │────▶│ Request      │────▶│ Check Org       │────▶│ Return       │
│         │     │ Plugin List  │     │ Membership      │     │ Filtered     │
└─────────┘     └─────────────┘     └─────────────────┘     │ Plugin List │
                                                          └─────────────┘
```

**Steps**:
1. User requests plugin list for their org
2. System verifies user's org membership
3. System filters catalog to only show plugins approved for that org's safe zone
4. Returns filtered list with security badges

### Flow 3: Appeal Process

```
┌─────────┐     ┌─────────────┐     ┌─────────────────┐     ┌─────────────┐     ┌─────────────┐
│ Author  │────▶│ Submit       │────▶│ Add to         │────▶│ Admin       │────▶│ Update       │
│         │     │ Appeal       │     │ Appeals Queue   │     │ Reviews      │     │ Analysis     │
└─────────┘     │ (with        │     │                 │     │ (Views       │     │ Result       │
                │  evidence)    │     └─────────────────┘     │  evidence,    │     │ (Approve/    │
                └─────────────┘                          │  makes        │     │ Reject)      │
                                                    │  decision)    │     └─────────────┘
                                                    └─────────────┘
```

**Steps**:
1. Author submits appeal with reason and evidence
2. Appeal added to admin queue
3. Admin reviews appeal, evidence, and analysis results
4. Admin makes decision:
   - Approve: Override analysis, accept plugin
   - Reject: Uphold analysis decision
5. System updates analysis result and notifies author
6. If approved, plugin accepted to catalog

---

## Technology Choices

### Async Queue: PostgreSQL-based
**Decision**: Use PostgreSQL as the queue backend (no external dependencies like RabbitMQ or Redis)

**Implementation**:
- `analysis_jobs` table with status tracking
- Workers poll for jobs with `SKIP LOCKED` pattern
- Priority-based ordering
- Automatic retry with exponential backoff

**Why**:
- No additional infrastructure required
- Transactional consistency with main database
- Simple to implement and maintain
- Scales sufficiently for expected load

### Sandbox: Docker (Default) / Firecracker (Future)
**Decision**: Use Docker for dynamic analysis sandbox, with Firecracker as future optimization

**Implementation**:
- Each analysis runs in isolated Docker container
- Container destroyed after analysis completion
- Resource limits enforced (CPU, memory, timeout)
- Network access restricted or disabled
- Filesystem access read-only except for temp directory

**Docker Configuration**:
```yaml
# docker-analysis.yml
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
```

**Why Docker**:
- Widely supported and understood
- Good isolation properties
- Easy to configure and manage
- Firecracker can be added later for better performance/security

### Static Analysis Tools
**Decision**: Use ESLint, Semgrep, Gitleaks, Trivy for static analysis

**Tools**:
- **ESLint**: JavaScript/TypeScript linting and code quality
- **Semgrep**: Static analysis for multiple languages, custom rules
- **Gitleaks**: Secret detection (API keys, tokens, passwords)
- **Trivy**: Vulnerability scanning for dependencies

**Configuration**:
- Each tool configured with security-focused rule sets
- Custom rules for plugin-specific patterns
- Severity levels mapped to scoring system

**Why These Tools**:
- ESLint: Industry standard for JS/TS
- Semgrep: Multi-language, customizable, open-source
- Gitleaks: Best-in-class secret detection
- Trivy: Comprehensive vulnerability database

---

## Data Flow & Integration Points

### CLI Integration
- CLI submits plugins to `/api/v1/plugins/submit`
- CLI polls for analysis status via `/api/v1/plugins/{id}/analysis`
- CLI displays security badges in listings
- CLI submits appeals via `/api/v1/plugins/{id}/appeal`

### Web UI Integration
- Web UI shows analysis results and security scores
- Web UI provides appeal submission form
- Web UI displays safe zone plugins for user's org
- Web UI shows reputation badges and leaderboard

### API Gateway Integration
- All requests routed through API Gateway
- Rate limiting applied at gateway level
- Authentication verified (service-to-service API keys)

### Analysis Service Integration
- Listens for jobs from PG queue
- Downloads plugin package from object storage
- Runs analysis in sandbox
- Stores results in PostgreSQL
- Updates plugin status

---

## Security Considerations

### Sandbox Isolation
- Each analysis runs in isolated container
- Containers destroyed after use
- No persistent state between analyses
- Resource limits prevent DoS

### Data Protection
- Plugin code only accessible to analysis workers
- Analysis results stored encrypted at rest
- Org data isolated by database design
- No cross-org data leakage

### Network Security
- Sandbox containers have no network access by default
- API Gateway enforces TLS
- Rate limiting prevents brute force attacks

### Secret Detection
- Gitleaks scans for API keys, tokens, passwords
- High severity findings cause automatic failure
- Secrets never logged or stored in plaintext

---

## Performance Considerations

### Analysis Timeout
- Total analysis time limited to 5 minutes per plugin
- Static analysis: 2 minute timeout
- Dynamic analysis: 3 minute timeout
- Queue processing: 100 concurrent jobs maximum

### Caching
- Analysis results cached for plugin versions
- Re-analysis only on new version submission
- Score calculations cached per plugin version

### Scaling
- Horizontal scaling of analysis workers
- PG queue handles job distribution
- Stateless workers can be added/removed dynamically

---

## Monitoring & Observability

### Metrics
- Queue length and processing time
- Analysis success/failure rates
- False positive rate (appeals approved / total rejections)
- Appeal resolution time
- System resource usage

### Logging
- All analysis steps logged with timestamps
- Errors logged with context
- No sensitive data in logs
- Log retention: 30 days

### Alerts
- Queue backlog > 100 jobs
- Analysis failure rate > 10%
- Sandbox startup failures
- Database connection issues

---

## Configuration

### Analysis Thresholds (Configurable)
```yaml
analysis:
  static_weight: 0.6
  dynamic_weight: 0.4
  pass_threshold: 80
  fail_threshold: 50
  review_threshold: 50
  
  static:
    eslint_weight: 0.25
    semgrep_weight: 0.25
    gitleaks_weight: 0.3
    trivy_weight: 0.2
    
  dynamic:
    behavior_weight: 1.0
```

### Rate Limits
```yaml
rate_limits:
  submission:
    per_ip: 10/hour
    per_author: 50/day
    burst: 5
    
  download:
    per_ip: 100/hour
    per_author: 1000/day
    
  appeal:
    per_author: 5/day
```

### Sandbox Configuration
```yaml
sandbox:
  docker:
    image: alpine:latest
    mem_limit: 512m
    cpu_shares: 512
    timeout: 300  # 5 minutes
    
  firecracker:
    enabled: false  # Future
    mem_limit: 256m
    vcpu: 1
```

---

## Migration Plan

### Phase 1: Core Infrastructure
1. Extend PostgreSQL schema with new tables
2. Create API Gateway with new endpoints
3. Implement basic CLI commands for submission
4. Set up PG-based queue

### Phase 2: Analysis Pipeline
1. Implement static analysis workers
2. Implement dynamic analysis sandbox
3. Implement scoring engine
4. Implement decision engine

### Phase 3: Safe Zone
1. Implement org management
2. Implement safe zone approval workflow
3. Implement safe zone filtering

### Phase 4: Control Center
1. Build admin dashboard
2. Implement metrics collection
3. Implement appeal resolution UI

### Phase 5: Reputation System
1. Implement karma system
2. Implement badges
3. Implement leaderboard

---

## Open Questions

### RESOLVED
1. ✅ **Sandbox Technology**: Docker for MVP, Firecracker as future optimization
2. ✅ **Queue Technology**: PostgreSQL-based (no external deps)
3. ✅ **Static Analysis Tools**: ESLint, Semgrep, Gitleaks, Trivy
4. ✅ **Scoring Weights**: Static 60%, Dynamic 40%

### Remaining
1. **Firecracker Integration**: When to implement as alternative to Docker?
   - *Default*: Phase 2 if Docker sandbox shows performance issues
   
2. **Manual Review Workflow**: How to handle plugins in review queue (50-79 score)?
   - *Default*: Admin dashboard with manual review interface
   
3. **Appeal Escalation**: What happens if author disagrees with appeal rejection?
   - *Default*: Final decision by super-admin (you)

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Sandbox escape | Use Docker with strict security options, monitor for CVEs |
| False positives | Appeal process with human review, configurable thresholds |
| Analysis performance | Timeout limits, resource constraints, horizontal scaling |
| Queue backlog | Monitor queue length, auto-scale workers, alert on backlog |
| Data leakage | Org isolation by design, regular security audits |
| Secret exposure | Gitleaks scanning, never log secrets, encrypt at rest |
| Rate limit bypass | IP-based + author-based limits, monitor for abuse |
| Analysis accuracy | Regular rule updates, multiple tools for cross-validation |
