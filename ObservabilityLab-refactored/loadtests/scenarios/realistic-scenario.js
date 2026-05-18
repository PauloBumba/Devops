/**
 * REALISTIC SCENARIO — Múltiplos perfis de usuário simultâneos
 * ─────────────────────────────────────────────────────────────
 * Simula tráfego real com 4 personas distintas executando em paralelo:
 *
 *  👤 Browser    — navega produtos (leitura pesada)
 *  🛒 Shopper    — lê e escreve (CRUD)
 *  🤖 ApiClient  — alta frequência, sem think time (integração)
 *  🔧 Ops        — monitora health e dashboard
 *
 * Esta é a configuração mais próxima de produção real.
 * Run: k6 run loadtests/scenarios/realistic-scenario.js
 */

import { check, group, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';
import { BASE_URL, DEFAULT_HEADERS, login, apiGet, apiPost, thinkTime } from '../utils/helpers.js';
import http from 'k6/http';

// ── Métricas por persona ──────────────────────────────────────────────────────
const browserLatency  = new Trend('persona_browser_latency');
const shopperLatency  = new Trend('persona_shopper_latency');
const apiClientErrors = new Rate('persona_apiclient_errors');

export const options = {
  scenarios: {
    // Browsers: muitos, comportamento humano (think time alto)
    browser_users: {
      executor:     'ramping-vus',
      startVUs:     0,
      stages: [
        { duration: '1m', target: 40 },
        { duration: '5m', target: 40 },
        { duration: '1m', target: 0  },
      ],
      exec: 'browserScenario',
      tags: { persona: 'browser' },
    },

    // Shoppers: menos, fazem leitura + escrita
    shopper_users: {
      executor:     'constant-vus',
      vus:          15,
      duration:     '7m',
      exec:         'shopperScenario',
      tags:         { persona: 'shopper' },
      startTime:    '30s',  // Começa depois do warm-up
    },

    // API Clients: poucos VUs mas zero think time (integrações)
    api_clients: {
      executor:     'constant-arrival-rate',
      rate:         20,        // 20 req/s
      timeUnit:     '1s',
      duration:     '7m',
      preAllocatedVUs: 5,
      maxVUs:          20,
      exec:         'apiClientScenario',
      tags:         { persona: 'api_client' },
    },

    // Ops: 1 VU verificando saúde continuamente
    ops_monitoring: {
      executor:  'constant-vus',
      vus:       1,
      duration:  '7m',
      exec:      'opsScenario',
      tags:      { persona: 'ops' },
    },

    // Chaos: injeção aleatória de carga anormal
    chaos_monkey: {
      executor:  'per-vu-iterations',
      vus:       2,
      iterations: 20,
      maxDuration: '7m',
      exec:      'chaosScenario',
      tags:      { persona: 'chaos' },
      startTime: '2m',
    },
  },

  thresholds: {
    'http_req_failed{persona:browser}':     ['rate<0.01'],
    'http_req_failed{persona:shopper}':     ['rate<0.02'],
    'http_req_failed{persona:api_client}':  ['rate<0.05'],
    'http_req_duration{persona:browser}':   ['p(95)<1000'],
    'http_req_duration{persona:shopper}':   ['p(95)<1500'],
    'http_req_duration{persona:api_client}':['p(95)<500'],
  },
};

// ── Persona: Browser (leitura, navegação lenta) ───────────────────────────────
export function browserScenario() {
  group('browse_catalog', () => {
    const list = apiGet('/products');
    browserLatency.add(list.timings.duration);
    check(list, { 'browser: list ok': (r) => r.status === 200 });
    thinkTime(2000);

    // Abre produto individual
    const id = Math.floor(Math.random() * 100) + 1;
    const detail = apiGet(`/products/cached/${id}`);
    browserLatency.add(detail.timings.duration);
    thinkTime(3000);
  });

  // 30% dos browsers também fazem login
  if (Math.random() < 0.3) {
    group('auth', () => {
      login('user', 'user123');
      thinkTime(1000);
    });
  }
}

// ── Persona: Shopper (leitura + escrita) ──────────────────────────────────────
export function shopperScenario() {
  group('shop_flow', () => {
    // Navega
    const list = apiGet('/products');
    shopperLatency.add(list.timings.duration);
    thinkTime(1000);

    // Cria produto (simula checkout/order)
    const create = apiPost('/products', {
      name:     `Order ${__VU}-${__ITER}-${Date.now()}`,
      price:    Math.round(Math.random() * 200 * 100) / 100,
      category: 'Orders',
    });
    shopperLatency.add(create.timings.duration);
    check(create, { 'shopper: create 201': (r) => r.status === 201 });
    thinkTime(500);
  });
}

// ── Persona: API Client (alta frequência, sem pausa) ──────────────────────────
export function apiClientScenario() {
  const endpoints = [
    '/products',
    '/products/1',
    '/products/cached/1',
    '/health/live',
  ];

  const endpoint = endpoints[Math.floor(Math.random() * endpoints.length)];
  const res = http.get(`${BASE_URL}${endpoint}`, {
    headers: DEFAULT_HEADERS,
    timeout: '3s',
  });

  apiClientErrors.add(res.status >= 400);
  check(res, { 'api_client: fast response': (r) => r.timings.duration < 300 });
}

// ── Persona: Ops (monitoramento contínuo) ────────────────────────────────────
export function opsScenario() {
  group('ops_checks', () => {
    const health = apiGet('/health');
    check(health, {
      'ops: not down':     (r) => r.status !== 503,
      'ops: responded':    (r) => r.status !== 0,
    });

    apiGet('/dashboard');
    apiGet('/dashboard/threads');
    apiGet('/dashboard/gc');
  });

  sleep(10);  // Ops verifica a cada 10s
}

// ── Persona: Chaos Monkey ─────────────────────────────────────────────────────
export function chaosScenario() {
  const chaosTargets = [
    () => apiGet('/lab/slow?delayMs=3000'),
    () => apiGet('/lab/cpu-burn?durationMs=500'),
    () => apiGet('/lab/chaos'),
    () => apiGet('/lab/memory-pressure?sizeMb=30'),
    () => { try { apiGet('/lab/error'); } catch {} },
  ];

  const attack = chaosTargets[Math.floor(Math.random() * chaosTargets.length)];
  attack();

  thinkTime(5000 + Math.random() * 10000);  // Chaos acontece esporadicamente
}

export function handleSummary(data) {
  return {
    'loadtests/results/realistic-summary.json': JSON.stringify(data, null, 2),
  };
}
