# Reputation System Specification

## ADDED Requirements

### Requirement: Track author reputation with karma system

The system SHALL maintain a reputation score (karma) for each plugin author based on their contributions and behavior.

#### Scenario: Author submits plugin that passes analysis
**WHEN** an author's plugin passes security analysis and is accepted to catalog

**THEN** the system SHALL:
- Award karma points to author
- Default: +10 points for passed plugin
- Check for level-up and award badges if applicable

#### Scenario: Author submits plugin that fails analysis
**WHEN** an author's plugin fails security analysis

**THEN** the system SHALL:
- Deduct karma points from author
- Default: -5 points for failed plugin
- Minimum karma: 0 (cannot go negative)

#### Scenario: Author's appeal is approved
**WHEN** an author's appeal is approved by admin

**THEN** the system SHALL:
- Award karma points to author
- Default: +15 points (reward for successful appeal)
- Reverse any previous deduction for the failed plugin

### Requirement: Award badges for achievements

The system SHALL award badges to authors for specific achievements.

#### Scenario: Author reaches karma milestone
**WHEN** an author's karma reaches 100

**THEN** the system SHALL award badge: "Security Champion"

**WHEN** an author's karma reaches 500

**THEN** the system SHALL award badge: "Trusted Author"

**WHEN** an author's karma reaches 1000

**THEN** the system SHALL award badge: "Security Expert"

#### Scenario: Author submits multiple high-quality plugins
**WHEN** an author has 10 plugins with score ≥ 90

**THEN** the system SHALL award badge: "Quality Contributor"

#### Scenario: Author has no security findings in any plugin
**WHEN** an author has submitted 5+ plugins with zero security findings

**THEN** the system SHALL award badge: "Clean Coder"

### Requirement: Display leaderboard of top authors

The system SHALL provide a leaderboard showing top authors by karma.

#### Scenario: View global leaderboard
**WHEN** a user requests the global leaderboard

**THEN** the system SHALL return:
- Top 100 authors by karma
- Rank, author name, karma, level, badges
- Paginated results

#### Scenario: View org-specific leaderboard
**WHEN** a user requests the leaderboard for their organization

**THEN** the system SHALL return:
- Top authors in that organization by karma
- Only authors who have contributed plugins to that org's safe zone

### Requirement: Show reputation in plugin listings

The system SHALL display author reputation information alongside plugins.

#### Scenario: View plugin in catalog
**WHEN** a user views a plugin in the catalog

**THEN** the system SHALL display:
- Author's karma score
- Author's level
- Author's badges (up to 3 most recent)
- Link to author's profile

#### Scenario: View author profile
**WHEN** a user clicks on an author's name

**THEN** the system SHALL display:
- Author's karma score and level
- All badges earned
- Plugin count
- Total downloads
- Recent activity
- Karma history (chart)

### Requirement: Apply reputation to safe zone approvals

The system SHALL use reputation to influence safe zone approval decisions.

#### Scenario: Auto-approve plugins from high-reputation authors
**GIVEN** configuration:
```yaml
safe_zone:
  auto_approve:
    enabled: true
    min_author_karma: 100
```

**WHEN** an author with karma ≥ 100 submits a plugin

**THEN** the system SHALL:
- Auto-approve plugin for all orgs (or configurable subset)
- Skip manual review
- Notify org admins of auto-approval

#### Scenario: Fast-track review for medium-reputation authors
**GIVEN** configuration:
```yaml
safe_zone:
  fast_track:
    enabled: true
    min_author_karma: 50
```

**WHEN** an author with karma ≥ 50 submits a plugin

**THEN** the system SHALL:
- Flag plugin for fast-track review
- Prioritize in review queue
- Notify admins of fast-track status

### Requirement: Prevent abuse of reputation system

The system SHALL prevent authors from gaming the reputation system.

#### Scenario: Detect and prevent karma farming
**WHEN** an author submits many low-quality plugins to farm karma

**THEN** the system SHALL:
- Detect pattern of rapid low-quality submissions
- Reduce or freeze karma gains
- Flag author for review

#### Scenario: Penalize for repeated security issues
**WHEN** an author has 3+ plugins rejected for same security issue

**THEN** the system SHALL:
- Increase karma deduction for subsequent similar issues
- Example: 1st failure: -5, 2nd: -10, 3rd+: -20

#### Scenario: Temporary karma freeze for abusive behavior
**WHEN** an author is flagged for abuse

**THEN** the system SHALL:
- Freeze karma at current level
- Prevent further gains or losses
- Duration: 7 days (configurable)

---

## Karma System Design

### Points System

| Action | Points | Notes |
|--------|--------|-------|
| Plugin passed analysis | +10 | Score ≥ pass_threshold |
| Plugin failed analysis | -5 | Score < fail_threshold |
| Appeal approved | +15 | Reversal of failure |
| Appeal rejected | 0 | No change |
| First plugin submission | +5 | Bonus for new authors |
| 10th plugin submission | +20 | Milestone bonus |
| 50th plugin submission | +50 | Milestone bonus |
| 100th plugin submission | +100 | Milestone bonus |
| Plugin with score ≥ 95 | +2 | Bonus for high quality |
| Plugin with zero findings | +3 | Perfect score bonus |
| Bug bounty submission | +50 | For reporting vulnerabilities |

### Deductions

| Action | Points | Notes |
|--------|--------|-------|
| Plugin failed analysis | -5 | Score < fail_threshold |
| Security vulnerability found | -10 | Per critical/high finding |
| Secret detected | -20 | Automatic deduction |
| Abuse detected | -50 | Per incident |
| Appeal rejected (abuse) | -10 | For frivolous appeals |

### Levels

| Level | Name | Min Karma | Badge |
|-------|------|-----------|-------|
| 1 | Newcomer | 0 | None |
| 2 | Contributor | 50 | Bronze Contributor |
| 3 | Trusted | 200 | Silver Contributor |
| 4 | Expert | 500 | Gold Contributor |
| 5 | Master | 1000 | Platinum Contributor |
| 6 | Legend | 2000 | Diamond Contributor |

### Minimum Karma
- Karma cannot go below 0
- Deductions stop at 0
- Example: Author with 3 karma, -5 deduction → 0 karma (not -2)

---

## Badges System

### Badge Types

#### Achievement Badges
| Badge | Requirement | Rarity |
|-------|-------------|--------|
| First Plugin | Submit first plugin | Common |
| Security Awareness | First plugin with zero findings | Common |
| Quality Contributor | 10 plugins with score ≥ 90 | Uncommon |
| Clean Coder | 5 plugins with zero findings | Uncommon |
| Security Champion | 100 karma | Rare |
| Trusted Author | 500 karma | Rare |
| Security Expert | 1000 karma | Epic |
| Bug Hunter | Report 5+ vulnerabilities | Epic |
| Early Adopter | Among first 100 authors | Legendary |

#### Streak Badges
| Badge | Requirement | Rarity |
|-------|-------------|--------|
| Weekly Contributor | Submit plugin every week for 4 weeks | Uncommon |
| Monthly Contributor | Submit plugin every month for 6 months | Rare |
| Perfect Month | All plugins in a month pass with zero findings | Rare |

#### Special Badges
| Badge | Requirement | Rarity |
|-------|-------------|--------|
| Open Source Hero | Plugin used by 1000+ users | Epic |
| Community Favorite | Plugin rated 4.5+ by 50+ users | Epic |
| Innovation Award | Plugin with novel security approach | Legendary |

### Badge Display
- Badges displayed on author profile
- Up to 3 badges shown in plugin listings
- All badges shown on leaderboard
- Badge tooltips show requirements

---

## Leaderboard Design

### Global Leaderboard
**Endpoint**: `GET /api/v1/reputation/leaderboard`

**Response**:
```json
{
  "data": [
    {
      "rank": 1,
      "authorId": "550e8400-e29b-41d4-a716-446655440000",
      "authorName": "Security Expert",
      "karma": 1500,
      "level": 5,
      "levelName": "Master",
      "badges": ["Security Expert", "Quality Contributor", "Clean Coder"],
      "pluginCount": 45,
      "totalDownloads": 25000,
      "avgScore": 92.5
    }
  ],
  "totalCount": 1000,
  "page": 1,
  "limit": 50,
  "totalPages": 20
}
```

### Org Leaderboard
**Endpoint**: `GET /api/v1/reputation/leaderboard?orgId={orgId}`

**Response**: Same structure as global, but filtered to org

### Time-Based Leaderboards
- **Weekly**: Reset every Monday
- **Monthly**: Reset on 1st of each month
- **All-Time**: Persistent

---

## API Contract

### GET /api/v1/reputation/authors/{authorId}
**Response (200 OK)**:
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Plugin Author",
  "email": "author@example.com",
  "karma": 850,
  "level": 4,
  "levelName": "Expert",
  "badges": [
    {
      "name": "Security Champion",
      "description": "Reached 100 karma",
      "rarity": "Rare",
      "earnedAt": "2024-01-01T00:00:00Z"
    },
    {
      "name": "Quality Contributor",
      "description": "10 plugins with score ≥ 90",
      "rarity": "Uncommon",
      "earnedAt": "2024-01-15T00:00:00Z"
    }
  ],
  "stats": {
    "pluginCount": 25,
    "totalDownloads": 15000,
    "avgScore": 88.5,
    "passRate": 0.92,
    "appealSuccessRate": 0.8
  },
  "karmaHistory": [
    {
      "date": "2024-01-01",
      "karma": 800,
      "change": +10,
      "reason": "Plugin passed analysis",
      "pluginId": "550e8400-e29b-41d4-a716-446655440001"
    }
  ],
  "joinedAt": "2024-01-01T00:00:00Z"
}
```

### GET /api/v1/reputation/leaderboard
**Query Parameters**:
- `orgId`: Filter by organization (optional)
- `timeRange`: `all-time`, `weekly`, `monthly` (default: `all-time`)
- `page`: Page number (default: 1)
- `limit`: Items per page (default: 50, max: 100)

**Response (200 OK)**: See above

### GET /api/v1/reputation/badges
**Response (200 OK)**:
```json
{
  "data": [
    {
      "name": "Security Champion",
      "description": "Reached 100 karma",
      "rarity": "Rare",
      "requirement": "karma >= 100",
      "icon": "security-champion.png"
    }
  ],
  "totalCount": 20
}
```

### GET /api/v1/reputation/authors/{authorId}/plugins
**Response (200 OK)**:
```json
{
  "data": [
    {
      "pluginId": "550e8400-e29b-41d4-a716-446655440001",
      "name": "My Plugin",
      "version": "1.0.0",
      "score": 92.5,
      "status": "passed",
      "downloads": 500,
      "karmaEarned": 12,
      "badgesEarned": ["Quality Contributor"]
    }
  ],
  "totalCount": 25
}
```

---

## Database Schema

```sql
-- Author reputation
CREATE TABLE author_reputation (
  author_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  karma INTEGER NOT NULL DEFAULT 0 CHECK (karma >= 0),
  level INTEGER NOT NULL DEFAULT 1,
  level_name TEXT NOT NULL DEFAULT 'Newcomer',
  plugin_count INTEGER NOT NULL DEFAULT 0,
  total_downloads BIGINT NOT NULL DEFAULT 0,
  avg_score DECIMAL(5,2) DEFAULT 0,
  pass_rate DECIMAL(5,2) DEFAULT 0,
  created_at TIMESTAMPTZ DEFAULT NOW(),
  updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Badges
CREATE TABLE badges (
  id SERIAL PRIMARY KEY,
  name TEXT NOT NULL UNIQUE,
  description TEXT NOT NULL,
  rarity TEXT NOT NULL CHECK (rarity IN ('Common', 'Uncommon', 'Rare', 'Epic', 'Legendary')),
  requirement TEXT NOT NULL,
  icon TEXT,
  created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Author badges
CREATE TABLE author_badges (
  author_id UUID NOT NULL REFERENCES author_reputation(author_id) ON DELETE CASCADE,
  badge_id INTEGER NOT NULL REFERENCES badges(id) ON DELETE CASCADE,
  earned_at TIMESTAMPTZ DEFAULT NOW(),
  PRIMARY KEY (author_id, badge_id)
);

-- Karma events
CREATE TABLE karma_events (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  author_id UUID NOT NULL REFERENCES author_reputation(author_id) ON DELETE CASCADE,
  event_type TEXT NOT NULL,
  points INTEGER NOT NULL,
  reason TEXT NOT NULL,
  plugin_id UUID REFERENCES plugins(id) ON DELETE SET NULL,
  appeal_id UUID REFERENCES appeals(id) ON DELETE SET NULL,
  created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Leaderboard cache (for performance)
CREATE TABLE leaderboard_cache (
  id SERIAL PRIMARY KEY,
  time_range TEXT NOT NULL CHECK (time_range IN ('all-time', 'weekly', 'monthly')),
  org_id UUID,  -- NULL for global
  rank INTEGER NOT NULL,
  author_id UUID NOT NULL,
  karma INTEGER NOT NULL,
  level INTEGER NOT NULL,
  plugin_count INTEGER NOT NULL,
  total_downloads BIGINT NOT NULL,
  cached_at TIMESTAMPTZ DEFAULT NOW(),
  UNIQUE (time_range, org_id, rank)
);

-- Indexes
CREATE INDEX idx_author_reputation_karma ON author_reputation(karma DESC);
CREATE INDEX idx_author_reputation_level ON author_reputation(level);
CREATE INDEX idx_author_badges_author ON author_badges(author_id);
CREATE INDEX idx_karma_events_author ON karma_events(author_id);
CREATE INDEX idx_karma_events_created ON karma_events(created_at DESC);
CREATE INDEX idx_leaderboard_cache ON leaderboard_cache(time_range, org_id, rank);
```

---

## Configuration

```yaml
reputation:
  # Point values
  points:
    plugin_passed: 10
    plugin_failed: -5
    appeal_approved: 15
    first_plugin: 5
    milestone_10: 20
    milestone_50: 50
    milestone_100: 100
    high_quality_bonus: 2
    zero_findings_bonus: 3
    bug_bounty: 50
    
  # Deductions
  deductions:
    plugin_failed: -5
    critical_finding: -10
    secret_detected: -20
    abuse: -50
    frivolous_appeal: -10
  
  # Levels
  levels:
    - name: "Newcomer"
      min_karma: 0
    - name: "Contributor"
      min_karma: 50
      badge: "Bronze Contributor"
    - name: "Trusted"
      min_karma: 200
      badge: "Silver Contributor"
    - name: "Expert"
      min_karma: 500
      badge: "Gold Contributor"
    - name: "Master"
      min_karma: 1000
      badge: "Platinum Contributor"
    - name: "Legend"
      min_karma: 2000
      badge: "Diamond Contributor"
  
  # Badges
  badges:
    security_champion:
      requirement: "karma >= 100"
      rarity: "Rare"
    trusted_author:
      requirement: "karma >= 500"
      rarity: "Rare"
    quality_contributor:
      requirement: "10 plugins with score >= 90"
      rarity: "Uncommon"
    clean_coder:
      requirement: "5 plugins with zero findings"
      rarity: "Uncommon"
  
  # Abuse detection
  abuse_detection:
    enabled: true
    rapid_submission_threshold: 10  # plugins per hour
    rapid_submission_deduction: -10
    repeat_issue_threshold: 3
    repeat_issue_multiplier: 2  # 2x deduction for repeat issues
    
  # Karma freeze
  freeze:
    enabled: true
    duration_days: 7
    threshold: 5  # flag after 5 abuse incidents
```

---

## Integration Points

### Plugin Submission
- On plugin submission: Create author record if not exists
- On analysis completion: Award or deduct karma based on result
- On appeal resolution: Award karma if approved

### Plugin Catalog
- Display author karma, level, badges in plugin listings
- Link to author profile
- Show reputation indicators

### Safe Zone
- Use author karma for auto-approval decisions
- Display author reputation in approval requests
- Prioritize high-reputation authors in review queue

### Control Center
- View author reputation in admin dashboards
- Manage reputation configuration
- View reputation analytics

---

## Error Messages

| Error | HTTP Status | Message |
|-------|-------------|---------|
| Author not found | 404 | "Author not found" |
| Invalid karma operation | 400 | "Invalid karma operation: {reason}" |
| Karma freeze active | 403 | "Karma is frozen until {date}" |
| Badge not found | 404 | "Badge not found" |
| Already has badge | 409 | "Author already has this badge" |

---

## Performance Requirements

- **Karma update**: ≤ 100ms
- **Leaderboard query**: ≤ 500ms for top 100
- **Author profile load**: ≤ 200ms
- **Badge check**: ≤ 50ms
- **Karma history**: ≤ 100ms for last 50 events
