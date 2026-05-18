# ─── Observability Lab — Makefile ────────────────────────────────────────────
# Uso: make <target>
# Exemplo: make up   make test-load   make logs

.PHONY: help up down restart build logs clean dev restore \
        migrate migration \
        health health-live health-ready \
        dashboard threads gc exceptions \
        test-load test-stress test-spike test-soak test-breakpoint test-realistic \
        grafana jaeger prometheus swagger adminer evolution \
        whatsapp-send whatsapp-instance \
        products products-cached \
        reset-db seed

# ── Variáveis ─────────────────────────────────────────────────────────────────
API_URL        := http://localhost:5000
EVOLUTION_URL  := http://localhost:8088
API_KEY        := observabilitylab-key
INSTANCE       := lab-instance

CYAN  := \033[0;36m
GREEN := \033[0;32m
YELLOW:= \033[0;33m
RESET := \033[0m

# ── Help ──────────────────────────────────────────────────────────────────────
help:
	@echo ""
	@echo "$(CYAN)╔══════════════════════════════════════════════════════════╗$(RESET)"
	@echo "$(CYAN)║           Observability Lab — Comandos                   ║$(RESET)"
	@echo "$(CYAN)╚══════════════════════════════════════════════════════════╝$(RESET)"
	@echo ""
	@echo "$(GREEN)Stack:$(RESET)"
	@echo "  make up               Sobe todo o stack"
	@echo "  make down             Para e remove containers + volumes"
	@echo "  make restart          Restart apenas da API"
	@echo "  make build            Rebuild da imagem da API (sem cache)"
	@echo "  make logs             Tail dos logs da API"
	@echo "  make dev              Só dependências via Docker + API local (dotnet run)"
	@echo ""
	@echo "$(GREEN)Banco de Dados:$(RESET)"
	@echo "  make migrate          Aplica migrations pendentes (EF Core)"
	@echo "  make migration NAME=X Cria nova migration"
	@echo "  make seed             Re-executa o DbSeeder manualmente"
	@echo "  make reset-db         Dropa e recria o banco (cuidado!)"
	@echo ""
	@echo "$(GREEN)Health Checks:$(RESET)"
	@echo "  make health           /health — status detalhado (postgres, redis, system)"
	@echo "  make health-live      /health/live — liveness probe"
	@echo "  make health-ready     /health/ready — readiness (somente críticos)"
	@echo ""
	@echo "$(GREEN)Dashboard / Diagnóstico:$(RESET)"
	@echo "  make dashboard        Snapshot do dashboard da API"
	@echo "  make threads          Status do thread pool"
	@echo "  make gc               Estatísticas do Garbage Collector"
	@echo "  make exceptions       Últimas exceções registradas"
	@echo ""
	@echo "$(GREEN)Produtos (Endpoints de negócio):$(RESET)"
	@echo "  make products         GET /products (lista todos)"
	@echo "  make products-cached  GET /products/cached (via Redis + MemoryCache)"
	@echo ""
	@echo "$(GREEN)WhatsApp / Evolution API:$(RESET)"
	@echo "  make whatsapp-instance  Lista instâncias da Evolution API"
	@echo "  make whatsapp-send TO=5511999999999 MSG='Olá!'  Envia mensagem"
	@echo ""
	@echo "$(GREEN)Testes de Carga (k6):$(RESET)"
	@echo "  make test-load        Load test       (100 VUs / 10 min)"
	@echo "  make test-stress      Stress test     (até 1000 VUs — acha o breakpoint)"
	@echo "  make test-spike       Spike test      (burst repentino)"
	@echo "  make test-soak        Soak test       (2h — detecta memory leak)"
	@echo "  make test-breakpoint  Breakpoint test (sobe VUs até o sistema cair)"
	@echo "  make test-realistic   Cenário realista (mix de endpoints)"
	@echo ""
	@echo "$(GREEN)Interfaces Web:$(RESET)"
	@echo "  make swagger          Abre Swagger/Docs  → $(API_URL)/docs"
	@echo "  make grafana          Abre Grafana        → http://localhost:3000"
	@echo "  make jaeger           Abre Jaeger UI      → http://localhost:16686"
	@echo "  make prometheus       Abre Prometheus     → http://localhost:9090"
	@echo "  make adminer          Abre Adminer        → http://localhost:8090"
	@echo "  make evolution        Abre Evolution API  → $(EVOLUTION_URL)"
	@echo ""

# ── Stack ─────────────────────────────────────────────────────────────────────

up:
	@echo "$(CYAN)Subindo Observability Lab...$(RESET)"
	docker compose up -d
	@echo ""
	@echo "$(GREEN)Stack rodando:$(RESET)"
	@echo "  API        → $(API_URL)"
	@echo "  Docs       → $(API_URL)/docs"
	@echo "  Health     → $(API_URL)/health"
	@echo "  Metrics    → $(API_URL)/metrics"
	@echo "  Dashboard  → $(API_URL)/dashboard"
	@echo "  Grafana    → http://localhost:3000  (admin / observability)"
	@echo "  Jaeger     → http://localhost:16686"
	@echo "  Prometheus → http://localhost:9090"
	@echo "  Adminer    → http://localhost:8090"
	@echo "  Evolution  → $(EVOLUTION_URL)"

down:
	docker compose down -v --remove-orphans

restart:
	docker compose restart api

build:
	docker compose build --no-cache api

logs:
	docker compose logs -f api

# ── Dev local (sem Docker para a API) ────────────────────────────────────────

dev:
	@echo "$(CYAN)Subindo dependências...$(RESET)"
	docker compose up -d postgres redis otelcollector prometheus grafana jaeger evolution-api
	@echo "$(CYAN)Iniciando API localmente...$(RESET)"
	cd src/Api && dotnet run

restore:
	dotnet restore ObservabilityLab.slnx

# ── Banco de Dados ────────────────────────────────────────────────────────────

migrate:
	cd src/Api && dotnet ef database update \
		--project ../Infrastructure \
		--startup-project .

migration:
	@test -n "$(NAME)" || (echo "$(YELLOW)Uso: make migration NAME=NomeDaMigration$(RESET)" && exit 1)
	cd src/Api && dotnet ef migrations add $(NAME) \
		--project ../Infrastructure \
		--startup-project . \
		--output-dir Migrations

reset-db:
	@echo "$(YELLOW)Atenção: isso vai dropar o banco!$(RESET)"
	docker compose exec postgres psql -U lab -c "DROP DATABASE IF EXISTS observabilitylab;"
	docker compose exec postgres psql -U lab -c "CREATE DATABASE observabilitylab;"
	$(MAKE) migrate

seed:
	@echo "$(CYAN)O seed roda automaticamente no startup da API.$(RESET)"
	@echo "$(CYAN)Para forçar, reinicie a API: make restart$(RESET)"

# ── Health Checks ─────────────────────────────────────────────────────────────
# Endpoints: /health | /health/live | /health/ready
# Verifica: postgres (tag=critical), redis, system-resources

health:
	@echo "$(CYAN)Health Check (postgres + redis + system):$(RESET)"
	@curl -s $(API_URL)/health | python3 -m json.tool 2>/dev/null || \
		curl -s $(API_URL)/health

health-live:
	@echo "$(CYAN)Liveness Probe:$(RESET)"
	@curl -s -o /dev/null -w "Status: %{http_code}\n" $(API_URL)/health/live

health-ready:
	@echo "$(CYAN)Readiness Probe (somente checks críticos):$(RESET)"
	@curl -s $(API_URL)/health/ready | python3 -m json.tool 2>/dev/null || \
		curl -s $(API_URL)/health/ready

# ── Dashboard / Diagnóstico ───────────────────────────────────────────────────
# Alimentado por: DashboardState, AppMetrics, AppDiagnostics

dashboard:
	@curl -s $(API_URL)/dashboard | python3 -m json.tool

threads:
	@echo "$(CYAN)Thread Pool Status:$(RESET)"
	@curl -s $(API_URL)/dashboard/threads | python3 -m json.tool

gc:
	@echo "$(CYAN)Garbage Collector Stats:$(RESET)"
	@curl -s $(API_URL)/dashboard/gc | python3 -m json.tool

exceptions:
	@echo "$(CYAN)Últimas Exceções:$(RESET)"
	@curl -s $(API_URL)/dashboard/exceptions | python3 -m json.tool

# ── Produtos ──────────────────────────────────────────────────────────────────

products:
	@echo "$(CYAN)GET /products$(RESET)"
	@curl -s $(API_URL)/products | python3 -m json.tool

products-cached:
	@echo "$(CYAN)GET /products/cached (TwoLayerCache: MemoryCache + Redis)$(RESET)"
	@curl -s $(API_URL)/products/cached | python3 -m json.tool

# ── WhatsApp / Evolution API ──────────────────────────────────────────────────
# Configurado em: EvolutionApiOptions (appsettings.json → seção "EvolutionApi")

whatsapp-instance:
	@echo "$(CYAN)Instâncias Evolution API:$(RESET)"
	@curl -s $(EVOLUTION_URL)/instance/fetchInstances \
		-H "apikey: $(API_KEY)" | python3 -m json.tool

whatsapp-send:
	@test -n "$(TO)"  || (echo "$(YELLOW)Uso: make whatsapp-send TO=5511999999999 MSG='Olá!'$(RESET)" && exit 1)
	@test -n "$(MSG)" || (echo "$(YELLOW)Defina MSG='sua mensagem'$(RESET)" && exit 1)
	@curl -s -X POST $(API_URL)/whatsapp/send-text \
		-H "Content-Type: application/json" \
		-d '{"instance":"$(INSTANCE)","number":"$(TO)","text":"$(MSG)"}' \
		| python3 -m json.tool

# ── Testes de Carga (k6) ──────────────────────────────────────────────────────

test-load:
	@echo "$(CYAN)Load Test — 100 VUs constantes / 10 min$(RESET)"
	@echo "$(CYAN)Monitore: make grafana$(RESET)"
	docker compose --profile loadtest run --rm \
		-e K6_SCRIPT=scripts/load-test.js k6

test-stress:
	@echo "$(CYAN)Stress Test — sobe até 1000 VUs para achar o breakpoint$(RESET)"
	@echo "$(CYAN)Observe: make dashboard | make health$(RESET)"
	docker compose --profile loadtest run --rm \
		-e K6_SCRIPT=scripts/stress-test.js k6

test-spike:
	@echo "$(CYAN)Spike Test — burst repentino de usuários$(RESET)"
	docker compose --profile loadtest run --rm \
		-e K6_SCRIPT=scripts/spike-test.js k6

test-soak:
	@echo "$(CYAN)Soak Test — 2h de carga contínua (detecta memory leak) ☕$(RESET)"
	docker compose --profile loadtest run --rm \
		-e K6_SCRIPT=scripts/soak-test.js k6

test-breakpoint:
	@echo "$(CYAN)Breakpoint Test — aumenta VUs até o sistema cair$(RESET)"
	docker compose --profile loadtest run --rm \
		-e K6_SCRIPT=scenarios/breakpoint-test.js k6

test-realistic:
	@echo "$(CYAN)Cenário Realista — mix de endpoints (products, auth, whatsapp)$(RESET)"
	docker compose --profile loadtest run --rm \
		-e K6_SCRIPT=scenarios/realistic-scenario.js k6

test-all: test-load test-stress

# ── Interfaces Web ────────────────────────────────────────────────────────────

swagger:
	open $(API_URL)/docs 2>/dev/null || xdg-open $(API_URL)/docs

grafana:
	open http://localhost:3000 2>/dev/null || xdg-open http://localhost:3000

jaeger:
	open http://localhost:16686 2>/dev/null || xdg-open http://localhost:16686

prometheus:
	open http://localhost:9090 2>/dev/null || xdg-open http://localhost:9090

adminer:
	open http://localhost:8090 2>/dev/null || xdg-open http://localhost:8090

evolution:
	open $(EVOLUTION_URL) 2>/dev/null || xdg-open $(EVOLUTION_URL)

# ── Limpeza ───────────────────────────────────────────────────────────────────

clean:
	docker compose down -v --remove-orphans
	rm -f loadtests/results/*.json
	rm -f logs/*.log
