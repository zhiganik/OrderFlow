-include .env
export
.DEFAULT_GOAL := help

# Recipes use POSIX shell syntax (inline VAR=value, backslash continuations).
# Force Git Bash so this works the same whether `make` is invoked from
# PowerShell, cmd.exe, or Git Bash itself. Two Windows-specific gotchas:
#  - GNU Make ignores SHELL for recipe execution and looks at MAKESHELL
#    instead; SHELL alone silently falls back to cmd.exe.
#  - `bash` on PATH can resolve to the WSL launcher stub in
#    C:\Windows\System32 instead of Git Bash, so we point at a real
#    bash.exe explicitly rather than relying on name resolution.
GIT_BASH := $(firstword $(wildcard \
	C:/Program\ Files/Git/bin/bash.exe \
	C:/Program\ Files/Git/usr/bin/bash.exe \
	D:/Git/bin/bash.exe \
	D:/Git/usr/bin/bash.exe))
ifeq ($(GIT_BASH),)
$(error Could not find Git Bash's bash.exe. Set GIT_BASH to its path, e.g. make GIT_BASH=D:/Git/usr/bin/bash.exe)
endif
SHELL := $(GIT_BASH)
MAKESHELL := $(GIT_BASH)
.SHELLFLAGS := -ec

COMPOSE := docker compose

.PHONY: help env build up down down-v restart rebuild ps logs \
        logs-gateway logs-identity logs-order logs-inventory logs-sqlserver logs-sb \
        sql-shell redis-cli sb-logs clean prune migrate-identity migrate-order

help: ## Show available commands
	@grep -E '^[a-zA-Z_-]+:.*## ' Makefile | sort | awk 'BEGIN {FS = ":.*## "}; {printf "  %-15s %s\n", $$1, $$2}'

env: ## Create .env from .env.example if it doesn't exist yet
	@test -f .env || cp .env.example .env

build: ## Build all service images
	$(COMPOSE) build

up: ## Start the full stack in the background
	$(COMPOSE) up -d

down: ## Stop and remove containers (keeps DB/Redis volumes)
	$(COMPOSE) down

down-v: ## Stop and remove containers AND volumes (wipes SQL Server/Redis data)
	$(COMPOSE) down -v

restart: ## Restart all containers without rebuilding
	$(COMPOSE) restart

rebuild: ## Rebuild images and recreate containers (after code/Dockerfile changes)
	$(COMPOSE) up -d --build --force-recreate

ps: ## Show container status
	$(COMPOSE) ps

logs: ## Tail logs for every service
	$(COMPOSE) logs -f

logs-gateway: ## Tail gateway logs
	$(COMPOSE) logs -f gateway

logs-identity: ## Tail identity-api logs
	$(COMPOSE) logs -f identity-api

logs-order: ## Tail order-api logs
	$(COMPOSE) logs -f order-api

logs-inventory: ## Tail inventory-api logs
	$(COMPOSE) logs -f inventory-api

logs-sqlserver: ## Tail sqlserver logs
	$(COMPOSE) logs -f sqlserver

logs-sb: ## Tail Service Bus emulator logs
	$(COMPOSE) logs -f sb-emulator

sql-shell: ## Open an interactive sqlcmd shell against the shared SQL Server
	MSYS_NO_PATHCONV=1 $(COMPOSE) exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$(MSSQL_SA_PASSWORD)" -C

redis-cli: ## Open an interactive redis-cli shell against Redis
	$(COMPOSE) exec redis redis-cli -a "$(REDIS_PASSWORD)"

clean: down-v ## Stop everything, wipe volumes, and remove built app images
	-docker image rm -f orderflow-identity-api orderflow-order-api orderflow-inventory-api orderflow-gateway

prune: ## Remove dangling images/build cache (careful: affects other projects' cache too)
	docker builder prune -f

migrate-identity: ## Apply pending Identity EF Core migrations (manual step, never automatic)
	ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=OrderFlowDb;User Id=sa;Password=$(MSSQL_SA_PASSWORD);TrustServerCertificate=True" \
	dotnet ef database update \
		--project services/identity/Identity.Infrastructure/Identity.Infrastructure.csproj \
		--startup-project services/identity/Identity.Api/Identity.Api.csproj \
		--context Identity.Infrastructure.Persistence.ApplicationIdentityDbContext

migrate-order: ## Apply pending Order EF Core migrations (manual step, never automatic)
	ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=OrderFlowDb;User Id=sa;Password=$(MSSQL_SA_PASSWORD);TrustServerCertificate=True" \
	dotnet ef database update \
		--project services/order/Order.Infrastructure/Order.Infrastructure.csproj \
		--startup-project services/order/Order.Api/Order.Api.csproj \
		--context Order.Infrastructure.Persistence.OrderDbContext
