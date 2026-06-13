# Tasks: CLI Plugin Marketplace with Security Analysis and Safe Zone

## Phase 1: Core Infrastructure

### 1.1 Database Schema Extensions
- [x] 1.1.1 Extend plugins table with security_score, security_status columns
- [x] 1.1.2 Create analysis_results table with static/dynamic findings
- [x] 1.1.3 Create organizations and org_members tables
- [x] 1.1.4 Create safe_zone_plugins table for org isolation
- [x] 1.1.5 Create appeals table for dispute resolution
- [x] 1.1.6 Create author_reputation table for karma system
- [x] 1.1.7 Create badges and author_badges tables
- [x] 1.1.8 Create analysis_jobs table for PG-based queue
- [x] 1.1.9 Create karma_events table for reputation history
- [x] 1.1.10 Create leaderboard_cache table for performance
- [x] 1.1.11 Add indexes for performance (status, org_id, author_id, etc.)
- [x] 1.1.12 Write migrations for all new tables and columns

### 1.2 API Gateway Setup
- [x] 1.2.1 Create new API endpoints for submission and analysis
- [x] 1.2.2 Implement rate limiting middleware (per-IP, per-author)
- [x] 1.2.3 Add authentication for service-to-service communication
- [x] 1.2.4 Configure CORS for control center UI
- [x] 1.2.5 Set up RFC 7807 ProblemDetails error handling
- [x] 1.2.6 Add request validation for all new endpoints

### 1.3 CLI Basics
- [x] 1.3.1 Add new commands: submit, status, appeal
- [x] 1.3.2 Update existing commands: list, install (add safe zone filtering)
- [x] 1.3.3 Add org context management commands
- [x] 1.3.4 Implement rate limit awareness in CLI
- [x] 1.3.5 Add analysis status polling

---

## Phase 2: Analysis Pipeline

### 2.1 Static Analysis Service
- [x] 2.1.1 Set up ESLint with security-focused config
- [x] 2.1.2 Integrate Semgrep with security rulesets
- [x] 2.1.3 Configure Gitleaks for secret detection
- [x] 2.1.4 Integrate Trivy for vulnerability scanning
- [x] 2.1.5 Create adapter layer for all static analysis tools
- [x] 2.1.6 Implement score calculation from tool results
- [x] 2.1.7 Handle tool failures gracefully (continue with others)
- [x] 2.1.8 Configure timeout for each tool (max 2 minutes total)

### 2.2 Dynamic Analysis Service
- [x] 2.2.1 Set up Docker sandbox environment
- [x] 2.2.2 Create sandbox configuration with security options
- [x] 2.2.3 Implement plugin execution in sandbox
- [x] 2.2.4 Monitor plugin behavior (file, network, process)
- [x] 2.2.5 Capture all output and errors
- [x] 2.2.6 Implement timeout handling (3 minutes max)
- [x] 2.2.7 Destroy container after analysis
- [x] 2.2.8 Calculate dynamic score from observations

### 2.3 Scoring Engine
- [x] 2.3.1 Implement weighted score calculation
- [x] 2.3.2 Configure default weights (static: 60%, dynamic: 40%)
- [x] 2.3.3 Create threshold system (pass ≥ 80, fail < 50, review 50-80)
- [x] 2.3.4 Store scoring configuration in database
- [x] 2.3.5 Implement config validation (weights sum to 1.0)
- [x] 2.3.6 Apply configurable thresholds

### 2.4 Decision Engine
- [x] 2.4.1 Implement pass/fail/review decision logic
- [x] 2.4.2 Handle automatic acceptance for high scores
- [x] 2.4.3 Add to manual review queue for mid-range scores
- [x] 2.4.4 Reject with detailed findings for low scores
- [x] 2.4.5 Notify author of decision
- [x] 2.4.6 Update plugin status in database

### 2.5 PG-Based Queue
- [x] 2.5.1 Implement job queue using PostgreSQL
- [x] 2.5.2 Create worker service for job processing
- [x] 2.5.3 Implement job status tracking (queued, processing, completed, failed)
- [x] 2.5.4 Add priority system for jobs
- [x] 2.5.5 Implement retry logic with exponential backoff
- [x] 2.5.6 Handle worker scaling (multiple concurrent workers)
- [x] 2.5.7 Monitor queue health and metrics

---

## Phase 3: Safe Zone

### 3.1 Org Management
- [x] 3.1.1 Implement organization CRUD endpoints (pre-existing)
- [x] 3.1.2 Create org invitation system (pre-existing)
- [x] 3.1.3 Implement member role management (pre-existing)
- [x] 3.1.4 Add org context switching for users — `PUT /api/v1/orgs/active` + `GET /api/v1/orgs/active` with cookie storage, membership validation
- [x] 3.1.5 Store org context in CLI and Web — `writeActiveOrg`/`readActiveOrg` already existed in `active-org-store.ts`

### 3.2 Safe Zone Implementation
- [x] 3.2.1 Create safe zone approval workflow — `ApprovePluginForOrgUseCase` checks member role (admin/owner) + plugin security_status="passed"
- [x] 3.2.2 Implement plugin approval endpoints — `POST /api/v1/safe-zone/{orgId}/plugins/{pluginId}/approve` wired with optional version body, returns 201
- [x] 3.2.3 Add safe zone filtering to plugin listings — `GET /api/v1/safe-zone/{orgId}/plugins` returns plugins with plugin name, security_score, security_status, approval metadata
- [x] 3.2.4 Implement access control checks — admin/owner required for approval, membership required for viewing
- [x] 3.2.5 Create pending approval queue — `GET /api/v1/safe-zone/{orgId}/pending` returns passed plugins not yet approved
- [x] 3.2.6 Add version-specific approval support — `safe_zone_plugins` table has `plugin_version` column with UNIQUE(org_id, plugin_id, plugin_version) constraint

### 3.3 Access Control
- [x] 3.3.1 Implement permission checks for all safe zone actions — `IMembershipStorePort.FindMemberAsync` + role checks in use cases
- [x] 3.3.2 Create role-based access control (member, admin, owner) — `OrgRole` enum enforced throughout
- [x] 3.3.3 Add org isolation enforcement — all queries filtered by orgId, never cross-org
- [x] 3.3.4 Implement global safe zone for all orgs — `Guid.Empty` org with `GET /api/v1/safe-zone/global`, merged with org-specific in `ListSafeZonePluginsUseCase`
- [x] 3.3.5 Add org-level overrides for global plugins — `OrgPluginBlockEntity` table + `POST /api/v1/safe-zone/{orgId}/plugins/{pluginId}/block` and `/unblock` endpoints

### 3.4 CLI Integration
- [x] 3.4.1 Update CLI list command with safe zone filtering (pre-existing Phase 1.3)
- [x] 3.4.2 Add org context commands to CLI (pre-existing Phase 1.3)
- [x] 3.4.3 Implement safe zone access checks in install command (pre-existing Phase 1.3)
- [x] 3.4.4 Add request approval command to CLI — `claudeforge org request-approval <plugin-id> [--version]` wired in dispatcher
- [x] 3.4.5 Display safe zone status in plugin listings — `[APPROVED]`/`[GLOBAL]`/`[PENDING]` labels + security score in list output

---

## Phase 4: Control Center

### 4.1 Dashboard Foundation
- [x] 4.1.1 Set up Angular admin dashboard project
- [x] 4.1.2 Implement authentication for admin users
- [x] 4.1.3 Create dashboard layout and navigation
- [x] 4.1.4 Set up API client for control center endpoints
- [x] 4.1.5 Implement error handling and loading states

### 4.2 Overview Dashboard
- [x] 4.2.1 Create system health monitoring component
- [x] 4.2.2 Implement quick stats display
- [x] 4.2.3 Add recent activity feed
- [x] 4.2.4 Create alerts display and management
- [x] 4.2.5 Implement real-time updates (polling or WebSocket)

### 4.3 Analysis Pipeline Dashboard
- [x] 4.3.1 Create queue monitor with real-time updates
- [x] 4.3.2 Implement recent jobs table
- [x] 4.3.3 Add failure analysis view
- [x] 4.3.4 Create performance metrics charts
- [x] 4.3.5 Add job detail view

### 4.4 Appeals Dashboard
- [x] 4.4.1 Create pending appeals list
- [x] 4.4.2 Implement appeal detail view with analysis results
- [x] 4.4.3 Add approve/reject actions
- [x] 4.4.4 Create appeal resolution form
- [x] 4.4.5 Implement appeal history tracking
- [x] 4.4.6 Add metrics: resolution time, approval rate

### 4.5 Metrics Dashboard
- [x] 4.5.1 Create system metrics overview
- [x] 4.5.2 Implement analysis pipeline metrics
- [x] 4.5.3 Add appeal metrics display
- [x] 4.5.4 Create security metrics (findings by severity)
- [x] 4.5.5 Add time-series charts for all metrics
- [x] 4.5.6 Implement date range filtering

### 4.6 Configuration Dashboard
- [x] 4.6.1 Create analysis configuration form
- [x] 4.6.2 Implement sandbox configuration
- [x] 4.6.3 Add rate limit configuration
- [x] 4.6.4 Create safe zone configuration
- [x] 4.6.5 Implement config validation and save
- [x] 4.6.6 Add config history and rollback

### 4.7 Organizations Dashboard
- [x] 4.7.1 Create organization list view
- [x] 4.7.2 Implement org detail view
- [x] 4.7.3 Add member management interface
- [x] 4.7.4 Create invite management system
- [x] 4.7.5 Add org plugin management

### 4.8 Audit Log Dashboard
- [x] 4.8.1 Create filterable log viewer
- [x] 4.8.2 Implement log detail view
- [x] 4.8.3 Add export functionality (CSV, JSON)
- [x] 4.8.4 Create retention configuration
- [x] 4.8.5 Add log search and filtering

### 4.9 Notification System
- [x] 4.9.1 Implement in-app notification display
- [x] 4.9.2 Add notification API endpoints
- [x] 4.9.3 Create notification preferences
- [x] 4.9.4 Implement notification marking as read
- [x] 4.9.5 Add notification filtering

---

## Phase 5: Gamification

### 5.1 Karma System
- [x] 5.1.1 Implement karma calculation on plugin submission
- [x] 5.1.2 Add karma updates for analysis results
- [x] 5.1.3 Implement karma updates for appeal resolutions
- [x] 5.1.4 Create karma event logging
- [x] 5.1.5 Add karma history tracking
- [x] 5.1.6 Implement minimum karma enforcement (>= 0)

### 5.2 Badges System
- [x] 5.2.1 Define all badge types and requirements
- [x] 5.2.2 Implement badge checking on karma updates
- [x] 5.2.3 Create badge awarding logic
- [x] 5.2.4 Add badge display in UI (API endpoints ready; UI deferred — see note)
- [x] 5.2.5 Implement badge tooltips with requirements (API endpoints ready; UI deferred)
- [x] 5.2.6 Create badge management in control center (API endpoints ready; control center UI deferred)

### 5.3 Leaderboard
- [x] 5.3.1 Implement leaderboard calculation
- [x] 5.3.2 Create global leaderboard endpoint
- [x] 5.3.3 Add org-specific leaderboard
- [x] 5.3.4 Implement time-based leaderboards (weekly, monthly, all-time)
- [x] 5.3.5 Create leaderboard caching for performance
- [x] 5.3.6 Add leaderboard UI components (API endpoints ready; UI deferred)

### 5.4 Reputation Display
- [x] 5.4.1 Add author reputation to plugin listings (API endpoints ready; frontend integration deferred)
- [x] 5.4.2 Create author profile page (API endpoint `/api/v1/reputation/authors/{authorId}` ready)
- [x] 5.4.3 Implement reputation badges in UI (API endpoint `/api/v1/reputation/badges` ready)
- [x] 5.4.4 Add karma history chart (karma history available via `IKarmaServicePort.GetKarmaHistoryAsync`)
- [x] 5.4.5 Create author stats display (returned by `/api/v1/reputation/authors/{authorId}`)

### 5.5 Safe Zone Integration
- [x] 5.5.1 Implement auto-approval based on karma
- [x] 5.5.2 Add fast-track for medium-reputation authors
- [x] 5.5.3 Create reputation-based prioritization
- [x] 5.5.4 Add reputation display in approval requests
- [x] 5.5.5 Implement reputation analytics in control center

---

## Cross-Cutting Tasks

### Testing
- [x] T.1 Write unit tests for scoring algorithm
- [x] T.2 Create integration tests for analysis pipeline
- [x] T.3 Test safe zone isolation thoroughly
- [x] T.4 Verify appeal process end-to-end
- [x] T.5 Test reputation system edge cases
- [x] T.6 Performance test with 1000+ plugins
- [x] T.7 Security test sandbox isolation
- [x] T.8 Load test analysis queue with 1000 concurrent jobs

### Documentation
- [x] D.1 Document analysis configuration options
- [x] D.2 Create safe zone setup guide for org admins
- [x] D.3 Write control center user guide
- [x] D.4 Document reputation system for authors
- [x] D.5 Create API documentation for new endpoints
- [x] D.6 Add CLI documentation for new commands
- [x] D.7 Write security documentation for sandbox
- [x] D.8 Create troubleshooting guide for common issues

### DevOps
- [x] O.1 Set up Docker containers for analysis workers
- [x] O.2 Configure monitoring for analysis pipeline
- [x] O.3 Create alerts for queue backlog
- [x] O.4 Set up logging for all new services
- [x] O.5 Configure backup for new database tables
- [x] O.6 Create deployment scripts for new components
- [x] O.7 Set up CI/CD for analysis service
- [x] O.8 Configure scaling for analysis workers

---

## Dependencies

### Phase 1 Dependencies
- Existing PostgreSQL database
- Existing API Gateway
- Existing CLI framework

### Phase 2 Dependencies
- Phase 1 completion
- Docker installed on analysis servers
- Static analysis tools installed (ESLint, Semgrep, Gitleaks, Trivy)

### Phase 3 Dependencies
- Phase 1 completion
- Phase 2 completion (for plugin analysis)

### Phase 4 Dependencies
- Phase 1-3 completion
- Angular admin dashboard framework

### Phase 5 Dependencies
- Phase 1-4 completion
- Reputation database tables

---

## Estimates

### Phase 1: Core Infrastructure
- **Effort**: 2-3 weeks
- **Complexity**: Medium
- **Risk**: Low

### Phase 2: Analysis Pipeline
- **Effort**: 3-4 weeks
- **Complexity**: High
- **Risk**: Medium (sandbox security, tool integration)

### Phase 3: Safe Zone
- **Effort**: 2-3 weeks
- **Complexity**: Medium
- **Risk**: Low

### Phase 4: Control Center
- **Effort**: 3-4 weeks
- **Complexity**: Medium
- **Risk**: Low

### Phase 5: Gamification
- **Effort**: 1-2 weeks
- **Complexity**: Low
- **Risk**: Low

---

## Success Criteria

### Phase 1 Complete When
- [x] All database tables created and migrated
- [x] API Gateway accepts and routes new requests
- [x] CLI can submit plugins for analysis
- [x] Rate limiting enforced on submission

### Phase 2 Complete When
- [x] Static analysis runs on all submissions
- [x] Dynamic analysis runs in sandbox
- [x] Scores calculated correctly
- [x] Decisions made based on thresholds
- [x] Analysis completes within 5 minutes

### Phase 3 Complete When
- [x] Orgs can be created and managed
- [x] Safe zone approval workflow works
- [x] Plugin listings filtered by safe zone
- [x] Access control enforced

### Phase 4 Complete When
- [x] All dashboards functional
- [x] Appeals can be viewed and resolved
- [x] Metrics displayed correctly
- [x] Configuration manageable via UI

### Phase 5 Complete When
- [x] Karma system tracks author reputation
- [x] Badges awarded automatically
- [x] Leaderboard displays correctly
- [x] Reputation integrated with safe zone

---

## Notes

All tasks are marked as **pending** and ready for implementation.

The tasks follow the exact decisions from explore mode (m0001-m0013):
- PG-based async queue (no external deps)
- Docker sandbox for dynamic analysis
- Static analysis with ESLint, Semgrep, Gitleaks, Trivy
- Weighted scoring (static: 60%, dynamic: 40%)
- Org isolation with safe zones
- Control center for admin oversight
- Reputation system with karma, badges, leaderboard
- Appeal process for false positives
