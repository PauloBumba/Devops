# ─── Observability Lab — Makefile ────────────────────────────────────────────
# Uso: make <target>
# Exemplo: make up   make test-load   make logs

.PHONY: help up down restart build logs clean \
        migrate seed \
        test-load test-stress test-spike test-soak \
        watch-dashboard watch-metrics \
        grafana jaeger prometheus swagger

# ── Cores para output ─────────────────────────────────────────────────────────
CYAN  := \033[0;36m
GREEN := \033[0;32m
RESET := \033[0m

## help: Mostra este menu
help:
	@echo ""
	@echo "$(CYAN)╔══════════════════════════════════════════════╗$(RESET)"
	@echo "$(CYAN)║      Observability Lab — Comandos            ║$(RESET)"
	@echo "$(CYAN)╚══════════════════════════════════════════════╝$(RESET)"
	@echo ""
	@echo "$(GREEN)Stack:$(RESET)"
	@echo "  make up             Sobe todo o stack (API + DB + Redis + OTel + Grafana)"
	@echo "  make down           Para e remove containers"
	@echo "  make restart        Restart da API"
	@echo "  make build          Rebuild da imagem da API"
	@echo "  make logs           Tail dos logs da API"
	@echo ""
	@echo "$(GREEN)Testes de Carga (k6):$(RESET)"
	@echo "  make test-load      Load test    (100 VUs / 10 min)"
	@echo "  make test-stress    Stress test  (até 1000 VUs — acha o breakpoint)"
	@echo "  make test-spike     Spike test   (burst repentino de usuários)"
	@echo "  make test-soak      Soak test    (2h — detecta memory leak)"
	@echo ""
	@echo "$(GREEN)Monitoramento:$(RESET)"
	@echo "  make dashboard      Abre snapshot do dashboard no terminal"
	@echo "  make health         Verifica health checks"
	@echo "  make grafana        Abre Grafana no browser"
	@echo "  make jaeger         Abre Jaeger UI no browser"
	@echo "  make prometheus     Abre Prometheus no browser"
	@echo "  make swagger        Abre Swagger UI no browser"
	@echo ""

# ── Stack ─────────────────────────────────────────────────────────────────────

## up: Sobe todo o stack
up:
	@echo "$(CYAN)Subindo Observability Lab...$(RESET)"
	docker compose up -d
	@echo ""
	@echo "$(GREEN)Stack rodando:$(RESET)"
	@echo "  API        → http://localhost:8080"
	@echo "  Swagger    → http://localhost:8080/swagger"
	@echo "  Dashboard  → http://localhost:8080/dashboard"
	@echo "  Grafana    → http://localhost:3000  (admin / observability)"
	@echo "  Jaeger     → http://localhost:16686"
	@echo "  Prometheus → http://localhost:9090"

## down: Para e remove containers
down:
	docker compose down -v

## restart: Restart da API sem derrubar o resto
restart:
	docker compose restart api

## build: Rebuild da imagem
build:
	docker compose build --no-cache api

## logs: Tail dos logs
logs:
	docker compose logs -f api

# ── Dev local (sem Docker) ────────────────────────────────────────────────────

## dev: Sobe só as dependências e roda a API localmente
dev:
	docker compose up -d postgres redis otelcollector prometheus grafana jaeger
	cd src/Api && dotnet run

## restore: Restaura pacotes NuGet
restore:
	dotnet restore ObservabilityLab.sln

## migrate: Aplica migrations do EF Core
migrate:
	cd src/Api && dotnet ef database update \
		--project ../Infrastructure \
		--startup-project .

## migration: Cria nova migration (use: make migration NAME=AddOrdersTable)
migration:
	cd src/Api && dotnet ef migrations add $(NAME) \
		--project ../Infrastructure \
		--startup-project . \
		--output-dir Migrations

# ── Testes de Carga ───────────────────────────────────────────────────────────

## test-load: Load test — 100 VUs constantes
test-load:
	@echo "$(CYAN)Iniciando Load Test (100 VUs)...$(RESET)"
	k6 run loadtests/scripts/load-test.js

## test-stress: Stress test — aumenta até quebrar
test-stress:
	@echo "$(CYAN)Iniciando Stress Test (até 1000 VUs)...$(RESET)"
	@echo "$(CYAN)Observe o dashboard: http://localhost:3000$(RESET)"
	k6 run loadtests/scripts/stress-test.js

## test-spike: Spike test — burst repentino
test-spike:
	@echo "$(CYAN)Iniciando Spike Test...$(RESET)"
	k6 run loadtests/scripts/spike-test.js

## test-soak: Soak test — 2 horas de carga contínua
test-soak:
	@echo "$(CYAN)Iniciando Soak Test (2h)... Vá tomar um café ☕$(RESET)"
	k6 run loadtests/scripts/soak-test.js

## test-all: Roda load e stress sequencialmente
test-all: test-load test-stress

# ── Monitoramento rápido ──────────────────────────────────────────────────────

## dashboard: Mostra snapshot do dashboard em JSON formatado
dashboard:
	@curl -s http://localhost:8080/dashboard | python3 -m json.tool || \
		curl -s http://localhost:8080/dashboard

## health: Verifica health checks
health:
	@echo "$(CYAN)Health Check:$(RESET)"
	@curl -s http://localhost:8080/health | python3 -m json.tool 2>/dev/null || \
		curl -s http://localhost:8080/health

## threads: Thread pool status
threads:
	@curl -s http://localhost:8080/dashboard/threads | python3 -m json.tool

## gc: GC statistics
gc:
	@curl -s http://localhost:8080/dashboard/gc | python3 -m json.tool

## exceptions: Últimas exceções
exceptions:
	@curl -s http://localhost:8080/dashboard/exceptions | python3 -m json.tool

# ── Abrir interfaces no browser ───────────────────────────────────────────────

## grafana: Abre Grafana
grafana:
	open http://localhost:3000 2>/dev/null || xdg-open http://localhost:3000

## jaeger: Abre Jaeger
jaeger:
	open http://localhost:16686 2>/dev/null || xdg-open http://localhost:16686

## prometheus: Abre Prometheus
prometheus:
	open http://localhost:9090 2>/dev/null || xdg-open http://localhost:9090

## swagger: Abre Swagger
swagger:
	open http://localhost:8080/swagger 2>/dev/null || xdg-open http://localhost:8080/swagger

# ── Limpeza ───────────────────────────────────────────────────────────────────

## clean: Remove volumes e resultados de testes
clean:
	docker compose down -v --remove-orphans
	rm -f loadtests/results/*.json
	rm -f logs/*.log

## reset-db: Dropa e recria o banco (cuidado!)
reset-db:
	docker compose exec postgres psql -U lab -c "DROP DATABASE IF EXISTS observabilitylab;"
	docker compose exec postgres psql -U lab -c "CREATE DATABASE observabilitylab;"
	make migrate
