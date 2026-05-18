# ObservabilityLab

Stack completa de observabilidade com ASP.NET Core 9, OpenTelemetry, Prometheus, Grafana e Jaeger.

---

## Pré-requisitos

| Ferramenta | Versão mínima | Para quê |
|---|---|---|
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | 24+ | Subir todos os serviços |
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/9.0) | 9.0 | Rodar a API localmente |
| [k6](https://k6.io/docs/get-started/installation/) *(opcional)* | qualquer | Testes de carga fora do Docker |

---

## 🚀 Opção 1 — Docker Compose (recomendado)

Sobe a API **e toda a stack** de observabilidade com um único comando.

```bash
# 1. Clone / extraia o projeto
cd ObservabilityLab-refactored

# 2. Sobe tudo em background
docker compose up -d

# 3. Acompanhe os logs da API
docker compose logs -f api
```

Aguarde ~20 segundos até o health check da API ficar verde.

### URLs disponíveis

| Serviço | URL | Credenciais |
|---|---|---|
| **API — Swagger UI** | http://localhost:5000/docs | — |
| **API — Dashboard interno** | http://localhost:5000/dashboard | — |
| **API — Health** | http://localhost:5000/health | — |
| **API — Métricas (Prometheus)** | http://localhost:5000/metrics | — |
| **Grafana** | http://localhost:3000 | admin / observability |
| **Prometheus** | http://localhost:9090 | — |
| **Jaeger (Tracing)** | http://localhost:16686 | — |
| **Adminer (Postgres UI)** | http://localhost:8090 | lab / lab_secret |
| **Evolution API (WhatsApp)** | http://localhost:8088 | — |

### Parar e limpar

```bash
# Parar sem apagar volumes (dados preservados)
docker compose down

# Parar E apagar volumes (banco zerado)
docker compose down -v
```

---

## 🛠️ Opção 2 — Rodar a API localmente (sem Docker para a API)

Use quando quiser debugar no Visual Studio / Rider com a stack de infraestrutura no Docker.

### 1. Sobe apenas a infraestrutura

```bash
docker compose up -d postgres redis otelcollector prometheus grafana jaeger
```

### 2. Rode a API

**Via CLI:**
```bash
cd src/Api
dotnet run
```

**Via Visual Studio / Rider:**
Pressione **F5** — o browser abrirá diretamente em `https://localhost:63085/docs` (Swagger UI).

> A variável `ASPNETCORE_ENVIRONMENT=Development` já está configurada no `launchSettings.json`.
> O banco Postgres precisa estar rodando antes de iniciar a API (ela aplica as migrations automaticamente).

### URLs (local)

| Endpoint | URL |
|---|---|
| Swagger UI | https://localhost:63085/docs |
| Dashboard | https://localhost:63085/dashboard |
| Health | https://localhost:63085/health |
| Métricas | https://localhost:63085/metrics |

---

## 🧪 Testes de carga com k6

```bash
# Load test padrão (via Docker)
docker compose --profile loadtest up k6

# Script específico
K6_SCRIPT=scenarios/realistic-scenario.js docker compose --profile loadtest up k6

# Outros scripts disponíveis
# scripts/load-test.js       — carga básica
# scripts/stress-test.js     — stress progressivo
# scripts/spike-test.js      — picos repentinos
# scripts/soak-test.js       — soak de longa duração
# scenarios/breakpoint-test.js
# scenarios/realistic-scenario.js
```

---

## 📋 Makefile (atalhos)

> No terminal, dentro da pasta `ObservabilityLab-refactored/`, rode `make help` para ver tudo.

### Stack

```bash
make up            # Sobe todo o stack (Docker Compose)
make down          # Para e remove containers + volumes
make restart       # Restart só da API
make build         # Rebuild da imagem da API sem cache
make logs          # Tail dos logs da API em tempo real
make dev           # Sobe dependências no Docker + API local (dotnet run)
make restore       # dotnet restore da solution
```

### Banco de dados

```bash
make migrate            # Aplica migrations pendentes (EF Core)
make migration NAME=X   # Cria nova migration com nome X
make reset-db           # Dropa e recria o banco (cuidado!)
make seed               # Dica: o seed roda automático no startup
```

### Health Checks

```bash
make health        # /health — postgres + redis + system (JSON detalhado)
make health-live   # /health/live — liveness probe
make health-ready  # /health/ready — readiness (somente críticos)
```

### Dashboard / Diagnóstico

```bash
make dashboard     # Snapshot completo da API (métricas em tempo real)
make threads       # Status do thread pool
make gc            # Estatísticas do Garbage Collector
make exceptions    # Últimas 20 exceções registradas
```

### Endpoints de negócio

```bash
make products         # GET /products
make products-cached  # GET /products/cached (Redis + MemoryCache)
```

### WhatsApp / Evolution API

```bash
make whatsapp-instance                             # Lista instâncias
make whatsapp-send TO=5511999999999 MSG='Olá!'    # Envia mensagem
```

### Testes de carga (k6)

```bash
make test-load        # 100 VUs constantes / 10 min
make test-stress      # Sobe até 1000 VUs — acha o breakpoint
make test-spike       # Burst repentino de usuários
make test-soak        # 2h de carga contínua (detecta memory leak) ☕
make test-breakpoint  # Aumenta VUs até o sistema cair
make test-realistic   # Mix realista de endpoints
```

### Abrir interfaces no browser

```bash
make swagger      # http://localhost:5000/docs
make grafana      # http://localhost:3000
make jaeger       # http://localhost:16686
make prometheus   # http://localhost:9090
make adminer      # http://localhost:8090
make evolution    # http://localhost:8088
```

---

## 🗂️ Estrutura do projeto

```
ObservabilityLab-refactored/
├── src/
│   ├── Api/                  # Minimal API — endpoints, middleware, Program.cs
│   ├── Application/          # Casos de uso / handlers
│   ├── Domain/               # Entidades e interfaces
│   ├── Infrastructure/       # EF Core, repositórios, Redis
│   ├── Observability/        # OpenTelemetry, métricas, dashboard interno
│   ├── Alerting/             # Canais de alerta (Email, WhatsApp, Telegram)
│   └── BuildingBlocks/       # Utilitários compartilhados
├── docker/
│   ├── grafana/              # Dashboards e datasources provisionados
│   ├── otel-config.yaml      # Configuração do OTel Collector
│   └── prometheus.yml        # Scrape configs
├── loadtests/                # Scripts k6
├── docker-compose.yml
├── Dockerfile
└── Makefile
```

---

## ❓ Problemas comuns

**A API sobe mas o browser abre em página em branco**
→ Certifique-se de que `launchSettings.json` contém `"launchUrl": "docs"`. Com a correção aplicada, o browser deve abrir diretamente no Swagger.

**Erro de conexão com o banco na inicialização**
→ O Postgres pode ainda estar subindo. Aguarde ~10 s e tente novamente, ou verifique com:
```bash
docker compose ps postgres
```

**`/docs` retorna 404 no Docker**
→ A URL correta no Docker é `http://localhost:5000/docs` (porta 5000, sem HTTPS).

**Grafana não mostra dados**
→ Verifique se o Prometheus está coletando: acesse http://localhost:9090/targets e confirme que `observabilitylab-api` está `UP`.
