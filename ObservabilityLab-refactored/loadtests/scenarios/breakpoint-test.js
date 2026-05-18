/**
 * BREAKPOINT TEST — Identificar o limite exato do sistema
 * ─────────────────────────────────────────────────────────────
 * Objetivo: descobrir precisamente em quantos VUs o sistema entra
 *           em colapso (error rate > 10% OU p99 > 5s).
 *
 * Diferente do stress test (ramping), este usa o executor
 * 'ramping-arrival-rate' para controlar RPS, não VUs.
 * Isso garante que medimos throughput real, não concorrência.
 *
 * Run: k6 run loadtests/scenarios/breakpoint-test.js
 *
 * Resultado esperado:
 *   - Em X RPS o sistema segura sem erros
 *   - Em Y RPS começa a degradar
 *   - Em Z RPS entra em colapso
 */

import { check } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';
import { BASE_URL, DEFAULT_HEADERS } from '../utils/helpers.js';
import http from 'k6/http';

// ── Métricas customizadas para o breakpoint ───────────────────────────────────
const breakpointErrors    = new Rate('breakpoint_error_rate');
const breakpointLatency   = new Trend('breakpoint_latency');
const droppedRequests     = new Counter('breakpoint_dropped');
const successfulRequests  = new Counter('breakpoint_successful');

export const options = {
  scenarios: {
    breakpoint: {
      executor:             'ramping-arrival-rate',
      startRate:            10,           // Começa com 10 req/s
      timeUnit:             '1s',
      preAllocatedVUs:      50,
      maxVUs:               1000,
      stages: [
        { duration: '1m',  target: 50  },   // 50 req/s
        { duration: '1m',  target: 100 },   // 100 req/s
        { duration: '1m',  target: 200 },   // 200 req/s
        { duration: '1m',  target: 300 },   // 300 req/s — possível limite
        { duration: '1m',  target: 500 },   // 500 req/s — stress pesado
        { duration: '2m',  target: 700 },   // 700 req/s — colapso esperado
      ],
    },
  },
  thresholds: {
    // Não definimos thresholds rígidos — queremos observar o comportamento
    // O teste não vai "falhar" propositalmente
    breakpoint_error_rate: ['rate<0.99'],  // Só para não travar o k6
  },
};

export default function () {
  const res = http.get(`${BASE_URL}/products`, {
    headers: DEFAULT_HEADERS,
    timeout: '5s',
    tags: { scenario: 'breakpoint' },
  });

  const success = res.status === 200;
  const timedOut = res.status === 0;

  breakpointErrors.add(!success);
  breakpointLatency.add(res.timings.duration);

  if (timedOut) {
    droppedRequests.add(1);
  } else if (success) {
    successfulRequests.add(1);
  }

  check(res, {
    'breakpoint: status ok':      (r) => r.status === 200,
    'breakpoint: < 5s':           (r) => r.timings.duration < 5000,
    'breakpoint: not timed out':  (r) => r.status !== 0,
  });
}

export function handleSummary(data) {
  const d          = data.metrics;
  const errorRate  = (d.breakpoint_error_rate?.values?.rate ?? 0) * 100;
  const p95        = d.breakpoint_latency?.values['p(95)'] ?? 0;
  const p99        = d.breakpoint_latency?.values['p(99)'] ?? 0;
  const dropped    = d.breakpoint_dropped?.values?.count ?? 0;
  const successful = d.breakpoint_successful?.values?.count ?? 0;
  const rps        = d.http_reqs?.values?.rate ?? 0;

  const statusEmoji = errorRate < 1  ? '✅ ESTÁVEL'  :
                      errorRate < 10 ? '⚠️  DEGRADADO' :
                      errorRate < 50 ? '🔴 COLAPSANDO' :
                                       '💀 COLAPSO TOTAL';

  return {
    'loadtests/results/breakpoint-summary.json': JSON.stringify(data, null, 2),
    stdout: `
╔══════════════════════════════════════════════════════════╗
║  BREAKPOINT TEST — ${statusEmoji.padEnd(36)}║
╠══════════════════════════════════════════════════════════╣
║  RPS atingido      : ${String(rps.toFixed(2) + ' req/s').padEnd(36)}║
║  Requisições OK    : ${String(successful).padEnd(36)}║
║  Requisições DROP  : ${String(dropped).padEnd(36)}║
║  Taxa de erro      : ${String(errorRate.toFixed(2) + '%').padEnd(36)}║
║  P95 latência      : ${String(p95.toFixed(0) + 'ms').padEnd(36)}║
║  P99 latência      : ${String(p99.toFixed(0) + 'ms').padEnd(36)}║
║  Max latência      : ${String((d.breakpoint_latency?.values?.max ?? 0).toFixed(0) + 'ms').padEnd(36)}║
╠══════════════════════════════════════════════════════════╣
║  💡 Verifique /dashboard/threads para saturação de pool  ║
║  💡 Verifique /dashboard/gc para pressão de memória      ║
║  💡 Verifique logs do EF Core para lock contention       ║
╚══════════════════════════════════════════════════════════╝
`
  };
}
