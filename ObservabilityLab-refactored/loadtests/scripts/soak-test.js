/**
 * SOAK TEST — Long-running endurance test
 * ─────────────────────────────────────────────────────────────
 * Goal: Detect memory leaks, resource exhaustion, and performance
 *       degradation over time. This is your "leave it running
 *       overnight" test.
 *
 * Pattern: Steady moderate load for 2+ hours
 * Duration: 2h (configurable via SOAK_DURATION env var)
 *
 * Run: k6 run loadtests/scripts/soak-test.js
 * Run 4h: SOAK_DURATION=4h k6 run loadtests/scripts/soak-test.js
 *
 * What to watch:
 *   - Memory climbing steadily? (memory leak)
 *   - GC Gen2 collections increasing? (large object heap pressure)
 *   - DB connection pool exhaustion after N hours?
 *   - Redis connection resets?
 *   - Latency drift (p95 at hour 1 vs hour 2)?
 *   - Error rate increasing over time?
 */

import { check, group, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';
import { login, getProducts, apiGet, apiPost, thinkTime } from '../utils/helpers.js';

const SOAK_DURATION  = __ENV.SOAK_DURATION  || '2h';
const SOAK_VUS       = parseInt(__ENV.SOAK_VUS || '30');

// Track latency trend over time — check for drift
const latencyTrend = new Trend('soak_latency_trend');
const cacheHitRate = new Rate('soak_cache_hit');

export const options = {
  scenarios: {
    soak: {
      executor:     'constant-vus',
      vus:          SOAK_VUS,
      duration:     SOAK_DURATION,
      gracefulStop: '60s',
    },
  },
  thresholds: {
    // Tight thresholds — at moderate load, we should see stable performance
    http_req_failed:     ['rate<0.01'],  // < 1% errors
    http_req_duration:   ['p(95)<1000'], // p95 < 1s throughout
    soak_latency_trend:  ['p(90)<800'],  // Catch latency drift
  },
};

// Track which iteration we're on to detect temporal patterns
let iterationCount = 0;

export default function () {
  iterationCount++;

  group('realistic_user_session', () => {

    // 1. Login
    group('auth', () => {
      if (__ITER % 50 === 0) {
        // Re-login every 50 iterations (simulate session expiry)
        login('user', 'user123');
      }
    });

    // 2. Browse products (DB-heavy)
    group('browse', () => {
      const res = getProducts();
      latencyTrend.add(res.timings.duration);
      thinkTime(500);

      // Individual product — alternates between cached and uncached
      const id = Math.floor(Math.random() * 100) + 1;
      const useCached = __ITER % 3 !== 0;

      if (useCached) {
        const cached = apiGet(`/products/cached/${id}`);
        // Detect if cached (faster response = likely cache hit)
        cacheHitRate.add(cached.timings.duration < 50);
      } else {
        apiGet(`/products/${id}`);
      }
      thinkTime(300);
    });

    // 3. Write operation (DB write load)
    group('write', () => {
      if (__ITER % 10 === 0) {
        apiPost('/products', {
          name:     `Soak Product ${__VU}-${__ITER}`,
          price:    Math.round(Math.random() * 500 * 100) / 100,
          category: 'SoakTest'
        });
      }
    });

    // 4. Monitor (simulates ops team watching the dashboard)
    group('monitoring', () => {
      if (__VU === 1) {
        // VU 1 always polls monitoring (ops)
        apiGet('/dashboard');
        apiGet('/health');
      }
    });
  });

  // Natural think time between user actions
  thinkTime(2000);
}

export function handleSummary(data) {
  const d      = data.metrics;
  const p95    = d.http_req_duration?.values['p(95)'] ?? 0;
  const errors = (d.http_req_failed?.values?.rate ?? 0) * 100;
  const status = errors < 1 && p95 < 1000 ? '✅ PASSED' : '❌ FAILED';

  return {
    'loadtests/results/soak-test-summary.json': JSON.stringify(data, null, 2),
    stdout: `
╔══════════════════════════════════════════════════════╗
║  SOAK TEST (${SOAK_DURATION}) — ${status.padEnd(36)}║
╠══════════════════════════════════════════════════════╣
║  Total Requests   : ${String(d.http_reqs?.values?.count ?? 0).padEnd(32)}║
║  Avg RPS          : ${String((d.http_reqs?.values?.rate ?? 0).toFixed(2)).padEnd(32)}║
║  Error Rate       : ${String(errors.toFixed(3) + '%').padEnd(32)}║
║  P50 Latency      : ${String((d.http_req_duration?.values['p(50)'] ?? 0).toFixed(0) + 'ms').padEnd(32)}║
║  P95 Latency      : ${String(p95.toFixed(0) + 'ms').padEnd(32)}║
║  P99 Latency      : ${String((d.http_req_duration?.values['p(99)'] ?? 0).toFixed(0) + 'ms').padEnd(32)}║
║  Soak P90 Drift   : ${String((d.soak_latency_trend?.values['p(90)'] ?? 0).toFixed(0) + 'ms').padEnd(32)}║
╚══════════════════════════════════════════════════════╝
  ⚡ Memory leak? Check 'system.memory.used_mb' in Grafana
  ⚡ GC pressure?  Check 'gc_gen2' counter trend
  ⚡ DB pool leak? Check DB connection count over time
`
  };
}
