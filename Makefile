-include .env
export
.DEFAULT_GOAL := help

COMPOSE := docker compose

.PHONY: help env build up down down-v restart rebuild ps logs \
        logs-gateway logs-identity logs-order logs-inventory logs-sqlserver logs-sb \
        sql-shell redis-cli sb-logs clean prune

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
