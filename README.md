# ClaudeForge — Open Plugin Marketplace for Claude Code

ClaudeForge is an open-source plugin marketplace for Claude Code agentic tools: skills, hooks, subagents,
slash commands, and MCP server configurations. It lets authors publish plugins and teams discover, install,
and manage them through a web UI or a CLI.

## Stack

| Layer | Technology |
|---|---|
| Frontend | Angular 19 standalone, OnPush, signal-based stores |
| Backend | .NET 8 Minimal API, Clean/Hexagonal architecture |
| Database | PostgreSQL 16 (full-text search via tsvector) |
| Vector search (opt-in) | Qdrant — gated behind `--profile semantic` |
| CLI | Node.js + TypeScript (`claude plugin <subcommand>`) |
| Infrastructure | OVH VM, OVH Object Storage (S3-compatible), Docker Compose |

## Monorepo Layout

```
/backend          .NET solution (ClaudeForge.sln) — API, Core, Application, Infrastructure, Tests
/frontend         Angular workspace — standalone, OnPush, signal stores
/cli              Node/TS CLI package — `claude plugin` subcommands
/plugin-template  Scaffolding templates (TS, Python, Go, Rust)
/seed-plugins     10 seed plugins for dev/demo
/docs             Markdown documentation pages (surfaced via Docs module)
/infra            Docker Compose, Dockerfiles, infrastructure config
/packages         Local plugin artifact storage (dev bind-mount, git-ignored)
/openspec         Architecture specs and change tracking
```

## One-Command Dev Stack

Prerequisites: Docker, Docker Compose

```bash
cp .env.example .env          # fill in dev values
make up                       # or: npm run dev:up
```

This starts:
- `api`      — .NET backend at http://localhost:5000
- `web`      — Angular dev server at http://localhost:4200
- `postgres` — PostgreSQL 16 at localhost:5432

Optional Qdrant (semantic search):
```bash
docker compose -f infra/docker-compose.yml --profile semantic up -d
```

## Development

```bash
# Install all deps
make install        # or: cd frontend && npm ci; cd ../cli && npm ci

# Format
make format

# Lint
make lint

# Build all
make build

# Run tests
cd backend && dotnet test
cd frontend && npm test
cd cli && npm test
```

## Architecture

- **Backend**: Clean Architecture — `Core` (domain hexagon, zero infra deps), `Application` (HTTP/validators),
  `Infrastructure` (EF Core, storage adapters). Modules: PluginCatalog, PluginPublishing,
  PluginDistribution, PluginSearch, Telemetry, Docs.
- **Frontend**: DDD domains (`catalog`, `search`, `dashboard`, `team-context`, `telemetry`, `docs`),
  each with `application/` (facades + signal stores), `domain/` (models, ports, rules), `infrastructure/`
  (HTTP adapters), `presentation/` (standalone OnPush components).
- **CLI**: OpenAPI-generated typed client, local registry at `~/.claude-plugins/`.

## License

Source code: [PolyForm Noncommercial License 1.0.0](LICENSE).
Hosted platform: open to all per [Terms of Service](TERMS.md).

## Author

Florian Michel — [GitHub: fmflurry](https://github.com/fmflurry)
