# Reputation System for Plugin Authors

## Overview

The reputation system incentivizes quality plugin development through **karma points**, **levels**, and **badges**. Authors earn rewards for submitting secure, well-vetted plugins and contributing to the ecosystem.

---

## Karma System

### How Points Are Earned and Lost

| Event | Points | Description |
|-------|--------|-------------|
| `plugin_submitted` | **+10** | Plugin submitted for analysis |
| `analysis_passed` | **+50** | Plugin passes security analysis (score >= pass_threshold) |
| `analysis_failed` | **-20** | Plugin fails security analysis (score < fail_threshold) |
| `analysis_review` | **+5** | Plugin enters review queue (50–79 score range) |
| `appeal_won` | **+30** | Admin approves author's appeal (reverses failure deduction) |
| `appeal_lost` | **-10** | Admin rejects author's appeal |
| `auto_approved` | **+5** | Plugin auto-approved due to high author karma |
| `badge_earned` | **+10** | Author earns a new badge |

### Karma Floor

Karma cannot go below **0**. Deductions stop at 0 — authors with 3 karma hit 0, not -17.

### Limits

- No maximum karma ceiling
- Karma is per-author, globally visible
- Initial karma for new authors: **0**

---

## Level Calculation

```
level = floor(karma / 100) + 1
```

| Karma | Level | Title |
|-------|-------|-------|
| 0–99 | 1 | Newcomer |
| 100–199 | 2 | Trusted |
| 200–299 | 3 | Established |
| 300–399 | 4 | Veteran |
| 400–499 | 5 | Expert |
| 500+ | 6+ | Master+ |

Level increases every 100 karma points. There is no level cap.

---

## Badge System

### Achievement Badges

| Badge | How to Earn | Points |
|-------|-------------|--------|
| **First Submission** | Submit your first plugin for analysis | +10 |
| **10 Submissions** | Submit 10 plugins to the marketplace | +10 |
| **100 Submissions** | Submit 100 plugins to the marketplace | +10 |
| **Clean Pass** | A plugin passes analysis with zero findings | +10 |
| **Security Champion** | Achieve 5+ consecutive clean passes across plugins | +10 |
| **Appeal Victor** | Win 3 appeals (admin-approved) | +10 |
| **Bug Hunter** | Submit valid vulnerability reports through the bug bounty program | +10 |
| **Bug Bounty** | Earn a bounty reward for a disclosed vulnerability | +10 |
| **Veteran** | Account age exceeds 1 year with active submissions | +10 |
| **Popular** | Plugin reaches 100+ total downloads | +10 |

### Rarity Tiers

| Rarity | Description |
|--------|-------------|
| Common | Easily earned by active authors |
| Uncommon | Requires consistent quality |
| Rare | Requires significant contributions |
| Epic | Requires exceptional achievement |
| Legendary | Reserved for the most outstanding authors |

### Badge Display

- Up to 3 most recent badges shown in plugin listings
- All badges shown on author profile page
- Badges displayed on leaderboard with tooltips showing requirements

---

## Auto-Approval

Authors with **karma >= 200** qualify for auto-approval:

- Plugins skip manual review queue entirely
- Even scores in the 50–79 range (`in_review`) are auto-passed
- Auto-approved plugins earn an additional **+5** karma

### Configuration

Auto-approval is controlled by the super-admin in Control Center:

```json
{
  "safe_zone": {
    "auto_approve": {
      "enabled": true,
      "min_author_karma": 200
    }
  }
}
```

---

## Priority Boosts

High-karma authors receive queue priority:

| Karma | Priority Boost |
|-------|----------------|
| >= 200 | Priority +20 (highest) |
| >= 100 | Priority +10 |

Priority affects analysis queue ordering — higher priority jobs are processed first.

---

## Viewing Reputation

### Author Profile

```http
GET /api/v1/reputation/authors/{authorId}
Authorization: Bearer <token>
```

**Response (200 OK)**:
```json
{
  "id": "author_u1v2w3x4",
  "name": "Plugin Author",
  "karma": 350,
  "level": 4,
  "levelName": "Veteran",
  "badges": [
    {
      "name": "10 Submissions",
      "description": "Submitted 10 plugins",
      "rarity": "Common",
      "earnedAt": "2026-03-15T10:00:00Z"
    },
    {
      "name": "Security Champion",
      "description": "5 consecutive clean passes",
      "rarity": "Rare",
      "earnedAt": "2026-05-01T10:00:00Z"
    }
  ],
  "stats": {
    "pluginCount": 15,
    "totalDownloads": 3200,
    "avgScore": 87.3,
    "passRate": 0.85,
    "appealSuccessRate": 0.75
  },
  "joinedAt": "2025-06-01T00:00:00Z"
}
```

### Leaderboard

```http
GET /api/v1/reputation/leaderboard
```

**Query Parameters**:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `orgId` | string (UUID) | — | Filter by organization |
| `timeRange` | enum | `all-time` | `all-time`, `weekly`, `monthly` |
| `page` | integer | 1 | Page number |
| `limit` | integer | 50 | Items per page (max 100) |

**Response (200 OK)**:
```json
{
  "data": [
    {
      "rank": 1,
      "authorId": "author_u1v2w3x4",
      "authorName": "Top Author",
      "karma": 1520,
      "level": 16,
      "badges": ["100 Submissions", "Security Champion", "Veteran"],
      "pluginCount": 120,
      "totalDownloads": 85000,
      "avgScore": 93.1
    }
  ],
  "totalCount": 500,
  "page": 1,
  "limit": 50,
  "totalPages": 10
}
```

### Author's Plugins

```http
GET /api/v1/reputation/authors/{authorId}/plugins
Authorization: Bearer <token>
```

Returns author's plugins with per-plugin karma earned and badges triggered.

### Available Badges Catalog

```http
GET /api/v1/reputation/badges
```

Returns all available badges with requirements and rarity.

---

## Abuse Prevention

### Karma Farming Detection

The system detects rapid low-quality submissions and applies penalties:

- **Threshold**: 10+ submissions per hour with fail/review outcomes
- **Penalty**: Karma freeze for 7 days (no gains or losses)
- **Flagged**: Author profile marked for admin review

### Repeat Issue Penalty

When an author repeatedly submits plugins with the same security issue:

- 1st failure: standard deduction (-20)
- 2nd consecutive same-issue failure: -30
- 3rd+: -40 per occurrence

### Karma Freeze

Admins can freeze an author's karma for investigations:

- Duration: configurable (default 7 days)
- During freeze: karma does not increase or decrease
- Plugins still processed normally
- Only super-admins can freeze/unfreeze

---

## Integration Points

| System | How Reputation Affects It |
|--------|---------------------------|
| Plugin Submission | High karma → priority queue placement |
| Analysis Pipeline | Karma >= 200 → auto-approval |
| Safe Zone | High karma → faster org approval via trust signals |
| Catalog | Badges shown in listings as trust indicators |
| Control Center | Admin sees author reputation when reviewing appeals |
| Appeals | High-karma authors' appeals may be flagged for priority review |
