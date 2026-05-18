/**
 * STRESS TEST — Find the breaking point
 * ─────────────────────────────────────────────────────────────
 * Goal: Gradually increase load until the system degrades or fails.
 *       Identify: max throughput, failure mode, and recovery.
 * Pattern: Progressive ramp — no ramp down (observe how it breaks)
 * Duration: ~15 minutes
 *
 * Run: k6 run loadtests/scripts/stress-test.js
 *
 * What to watch:
 *   - At what VU count does p95 latency exceed 2s?
 *   - At what VU count does error rate spike?
 *   - Does the system recover after peak, or stays degraded?
 *   - ThreadPool exhaustion? DB connection pool exhaustion?
 */

import { check, group, sleep } from 'k6';
import { Trend, Counter, Rate } from 'k6/metrics';
import { BASE_URL, DEFAULT_HEADERS, login, getProducts, apiGet, apiPost, thinkTime } from '../utils/helpers.js';

const breakingPointVus = new Counter('stress_breaking_point_vu');
const degradationStart = new Trend('stress_degradation_onset_ms');

export const options = {
  scenarios: {
    stress: {
      executor:     'ramping-vus',
      startVUs:     0,
      gracefulStop: '30s',
      stages: [
        { duration: '2m',  target: 50  },   // Warm-up
        { duration: '2m',  target: 100 },   // Normal load
        { duration: '2m',  target: 200 },   // 2x normal
        { duration: '2m',  target: 400 },   // Heavy load
        { duration: '2m',  target: 600 },   // Very heavy
        { duration: '2m',  target: 800 },   // Near-breakpoint
        { duration: '3m',  target: 1000 },  // Maximum pressure
        { duration: '1m',  target: 0   },   // Sudden drop — observe recovery
      ],
    },
  },
  thresholds: {
    // Softer thresholds for stress test — we EXPECT failures
    http_req_failed:   ['rate<0.20'],    // Accept up to 20% errors
    http_req_duration: ['p(99)<10000'],  // P99 < 10s
  },
};

export default function () {
  const vuId = __VU;

  // Mix of endpoint types to simulate real traffic
  const scenario = vuId % 5;

  if (scenario === 0) {
    // Auth heavy
    group('auth', () => {
      login('user', 'user123');
      sleep(0.1);
    });

  } else if (scenario === 1) {
    // DB heavy
    group('db_reads', () => {
      for (let i = 0; i < 3; i++) {
        const res = apiGet('/products');
        if (res.timings.duration > 2000) {
          degradationStart.add(res.timings.duration);
        }
        sleep(0.05);
      }
    });

  } else if (scenario === 2) {
    // Mixed CRUD
    group('crud', () => {
      apiGet('/products');
      apiPost('/products', {
        name:     `Stress Product ${Math.random().toFixed(4)}`,
        price:    Math.round(Math.random() * 1000 * 100) / 100,
        category: 'StressTest'
      });
    });

  } else if (scenario === 3) {
    // Slow endpoint pressure
    group('slow', () => {
      const res = apiGet('/lab/slow?delayMs=500');
      check(res, { 'slow: responded': (r) => r.status !== 0 });
    });

  } else {
    // Dashboard polling (internal monitoring endpoints)
    group('monitoring', () => {
      apiGet('/dashboard');
      apiGet('/health');
      apiGet('/metrics');
    });
  }

  // Minimal think time — maximize pressure
  sleep(0.1 + Math.random() * 0.2);
}

export function handleSummary(data) {
  const errorRate = (data.metrics.http_req_failed?.values?.rate ?? 0) * 100;
  const p95       = data.metrics.http_req_duration?.values['p(95)'] ?? 0;

  const breakingPoint = errorRate > 5 ? '⚠️  DEGRADED' :
                        errorRate > 15 ? '🔴 BROKEN' : '✅ STABLE';

  return {
    'loadtests/results/stress-test-summary.json': JSON.stringify(data, null, 2),
    stdout: `
╔══════════════════════════════════════════════════════╗
║  STRESS TEST — Result: ${breakingPoint.padEnd(30)}║
╠══════════════════════════════════════════════════════╣
║  Total Requests : ${String(data.metrics.http_reqs?.values?.count ?? 0).padEnd(34)}║
║  Peak RPS       : ${String((data.metrics.http_reqs?.values?.rate ?? 0).toFixed(2)).padEnd(34)}║
║  Error Rate     : ${String(errorRate.toFixed(2) + '%').padEnd(34)}║
║  P95 Latency    : ${String(p95.toFixed(0) + 'ms').padEnd(34)}║
║  Max Latency    : ${String((data.metrics.http_req_duration?.values?.max ?? 0).toFixed(0) + 'ms').padEnd(34)}║
╚══════════════════════════════════════════════════════╝
`
  };
}
