# Sprint Planning Assistant

Assists product managers with sprint planning, estimation, and backlog grooming.

## Overview

The Sprint Planning Assistant uses AI-driven estimation and backlog analysis to help
product managers run more effective sprint planning ceremonies with actionable story
point suggestions and priority recommendations.

## Installation

```bash
claude plugin install sprint-planning-assistant
```

## Configuration

No additional configuration required.

## Usage

Invoke as a skill before or during sprint planning sessions to get AI-powered estimates
and backlog prioritization suggestions.

## API

- `estimate(story: UserStory) -> StoryPointEstimate` — estimate story points
- `groom(backlog: list[UserStory]) -> GroomedBacklog` — sort and prioritize a backlog

## Contributing

See the ClaudeForge contributor guide for contribution guidelines.

## License

MIT
