/**
 * LOAD TEST — Steady load simulation
 * ─────────────────────────────────────────────────────────────
 * Goal: Validate system behavior under normal production load.
 * Pattern: Ramp up → Sustain 100 VUs → Ramp down
 * Duration: ~10 minutes
 *
 * Run: k6 run loadtests/scripts/load-test.js
 * Run with env: BASE_URL=http://myapp:8080 k6 run load-test.js
 */

import { check, group, sleep } from 'k6';
import { BASE_THRESHOLDS, login, getProducts, hitSlowEndpoint, apiGet, thinkTime } from '../utils/helpers.js';

// ─── Scenario config ──────────────────────────────────────────────────────────
export const options = {
  scenarios: {
    steady_load: {
      executor:         'ramping-vus',
      startVUs:         0,
      gracefulStop:     '30s',
      stages: [
        { duration: '1m',  target: 25  },  // Warm-up: ramp to 25 VUs
        { duration: '1m',  target: 100 },  // Ramp to full load
        { duration: '5m',  target: 100 },  // Sustain 100 VUs
        { duration: '2m',  target: 50  },  // Partial ramp down
        { duration: '1m',  target: 0   },  // Ramp down
      ],
    },
  },
  thresholds: {
    ...BASE_THRESHOLDS,
    http_req_duration: [
      'p(50)<200',    // Median < 200ms
      'p(95)<800',    // 95% < 800ms
      'p(99)<2000',   // 99% < 2s
    ],
    http_req_failed: ['rate<0.01'], // Less than 1% errors under normal load
  },
};

// ─── Default function (one "virtual user" lifecycle) ─────────────────────────
export default function () {
  const userId = __VU;

  group('Auth flow', () => {
    const users = ['admin', 'user', 'tester'];
    const user  = users[userId % users.length];
    const pass  = user === 'admin' ? 'admin123' : `${user}123`;

    login(user, pass);
    thinkTime(500);
  });

  group('Browse products', () => {
    getProducts();
    thinkTime(300);

    // Get a random product by ID
    const id = Math.floor(Math.random() * 100) + 1;
    const res = apiGet(`/products/${id}`);
    check(res, { 'product detail: 200 or 404': (r) => [200, 404].includes(r.status) });
    thinkTime(200);

    // Get cached product
    apiGet(`/products/cached/${id}`);
  });

  group('Health & monitoring', () => {
    const health = apiGet('/health');
    check(health, { 'health: not degraded': (r) => r.status !== 503 });

    apiGet('/dashboard');
  });

  // Think time between complete user cycles
  thinkTime(1000);
}

export function handleSummary(data) {
  return {
    'loadtests/results/load-test-summary.json': JSON.stringify(data, null, 2),
    stdout: formatSummary(data, 'LOAD TEST'),
  };
}

function formatSummary(data, label) {
  const d = data.metrics;
  return `
╔══════════════════════════════════════════════════════╗
║  ${label.padEnd(52)}║
╠══════════════════════════════════════════════════════╣
║  Total Requests : ${String(d.http_reqs?.values?.count ?? 0).padEnd(34)}║
║  RPS (avg)      : ${String((d.http_reqs?.values?.rate ?? 0).toFixed(2)).padEnd(34)}║
║  Error Rate     : ${String(((d.http_req_failed?.values?.rate ?? 0) * 100).toFixed(2) + '%').padEnd(34)}║
║  P50 Latency    : ${String((d.http_req_duration?.values['p(50)'] ?? 0).toFixed(0) + 'ms').padEnd(34)}║
║  P95 Latency    : ${String((d.http_req_duration?.values['p(95)'] ?? 0).toFixed(0) + 'ms').padEnd(34)}║
║  P99 Latency    : ${String((d.http_req_duration?.values['p(99)'] ?? 0).toFixed(0) + 'ms').padEnd(34)}║
║  Max Latency    : ${String((d.http_req_duration?.values?.max ?? 0).toFixed(0) + 'ms').padEnd(34)}║
╚══════════════════════════════════════════════════════╝
`;
}
