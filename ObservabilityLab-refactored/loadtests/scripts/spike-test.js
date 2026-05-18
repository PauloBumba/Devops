/**
 * SPIKE TEST — Sudden traffic burst
 * ─────────────────────────────────────────────────────────────
 * Goal: Simulate a flash crowd event (viral post, sale, etc.)
 *       Validates auto-scaling behavior and circuit breakers.
 * Pattern: Normal → Instant spike → Drop → Normal → Second spike
 *
 * Run: k6 run loadtests/scripts/spike-test.js
 */

import { check, sleep } from 'k6';
import { login, getProducts, apiGet, BASE_THRESHOLDS } from '../utils/helpers.js';

export const options = {
  scenarios: {
    spike: {
      executor:     'ramping-vus',
      startVUs:     0,
      gracefulStop: '10s',
      stages: [
        { duration: '30s', target: 20  },   // Baseline
        { duration: '10s', target: 500 },   // SPIKE — instant surge
        { duration: '1m',  target: 500 },   // Hold spike
        { duration: '10s', target: 20  },   // Drop back
        { duration: '2m',  target: 20  },   // Recovery observation
        { duration: '10s', target: 300 },   // Second spike (smaller)
        { duration: '30s', target: 300 },   // Hold
        { duration: '30s', target: 0   },   // Ramp down
      ],
    },
  },
  thresholds: {
    // Spike thresholds — some degradation is acceptable
    http_req_failed:   ['rate<0.15'],
    http_req_duration: ['p(95)<5000'],
    http_req_duration: ['p(99)<10000'],
  },
};

export default function () {
  // During a spike, simulate users doing the most common actions
  const res = getProducts();
  check(res, { 'spike: products responded': (r) => r.status !== 0 });

  if (__VU % 10 === 0) {
    // 10% of VUs try to login
    login('user', 'user123');
  }

  if (__VU % 5 === 0) {
    // 20% check health
    apiGet('/health/live');
  }

  sleep(0.1);
}

export function handleSummary(data) {
  return {
    'loadtests/results/spike-test-summary.json': JSON.stringify(data, null, 2),
  };
}
