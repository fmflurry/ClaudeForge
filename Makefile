# ClaudeForge Monorepo — root convenience targets
.PHONY: install build lint format format-check test up down logs help

# ─── Setup ────────────────────────────────────────────────────────────────────
install: ## Install all dependencies (Node workspaces)
	npm install

# ─── Build ────────────────────────────────────────────────────────────────────
build: build-backend build-frontend build-cli ## Build all packages

build-backend: ## Build .NET backend
	cd backend && dotnet build --nologo -clp:ErrorsOnly

build-frontend: ## Build Angular frontend
	cd frontend && npm run build

build-cli: ## Build CLI package
	cd cli && npm run build

# ─── Lint ─────────────────────────────────────────────────────────────────────
lint: lint-backend lint-frontend lint-cli ## Lint all packages

lint-backend: ## Run dotnet format --verify-no-changes
	cd backend && dotnet format --verify-no-changes --no-restore

lint-frontend: ## Run ESLint on Angular project
	cd frontend && npm run lint

lint-cli: ## Run ESLint on CLI package
	cd cli && npm run lint

# ─── Format ───────────────────────────────────────────────────────────────────
format: format-backend format-frontend format-cli ## Format all packages

format-backend: ## Run dotnet format
	cd backend && dotnet format --no-restore

format-frontend: ## Run Prettier on Angular project
	cd frontend && npm run format

format-cli: ## Run Prettier on CLI package
	cd cli && npm run format

format-check: format-check-frontend format-check-cli ## Check formatting (no changes)

format-check-frontend: ## Check Prettier formatting on Angular project
	cd frontend && npm run format:check

format-check-cli: ## Check Prettier formatting on CLI package
	cd cli && npm run format:check

# ─── Test ─────────────────────────────────────────────────────────────────────
test: test-backend test-frontend test-cli ## Run all test suites

test-backend: ## Run .NET xUnit tests
	cd backend && dotnet test --nologo

test-frontend: ## Run Angular tests
	cd frontend && npm test

test-cli: ## Run CLI tests
	cd cli && npm test

# ─── Docker Dev Stack ─────────────────────────────────────────────────────────
up: ## Start the full dev stack (api + web + postgres)
	docker compose -f infra/docker-compose.yml up -d

down: ## Stop the dev stack
	docker compose -f infra/docker-compose.yml down

logs: ## Follow logs from the dev stack
	docker compose -f infra/docker-compose.yml logs -f

up-semantic: ## Start dev stack WITH Qdrant (optional semantic search)
	docker compose -f infra/docker-compose.yml --profile semantic up -d

# ─── Help ─────────────────────────────────────────────────────────────────────
help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | \
		awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-22s\033[0m %s\n", $$1, $$2}'
