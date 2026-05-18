// loadtests/utils/helpers.js
// Shared utilities for all k6 test scripts

import { check, sleep } from 'k6';
import http from 'k6/http';
import { Rate, Trend, Counter } from 'k6/metrics';

// ─── Base URL (override with K6_BASE_URL env var) ────────────────────────────
export const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';

// ─── Custom metrics ───────────────────────────────────────────────────────────
export const errorRate        = new Rate('custom_error_rate');
export const slowRequests     = new Rate('custom_slow_requests');
export const loginDuration    = new Trend('custom_login_duration_ms');
export const productsDuration = new Trend('custom_products_duration_ms');
export const dbDuration       = new Trend('custom_db_duration_ms');
export const totalRequests    = new Counter('custom_total_requests');

// ─── Default headers ──────────────────────────────────────────────────────────
export const DEFAULT_HEADERS = {
  'Content-Type': 'application/json',
  'Accept':       'application/json',
};

// ─── Shared thresholds (import and merge in individual scripts) ───────────────
export const BASE_THRESHOLDS = {
  http_req_failed:            ['rate<0.05'],   // < 5% error rate
  http_req_duration:          ['p(95)<2000'],  // 95% of requests < 2s
  custom_error_rate:          ['rate<0.05'],
  custom_slow_requests:       ['rate<0.1'],
};

// ─── Helper functions ─────────────────────────────────────────────────────────

/** GET wrapper with automatic metrics recording */
export function apiGet(path, params = {}) {
  const res = http.get(`${BASE_URL}${path}`, {
    headers: DEFAULT_HEADERS,
    tags: { endpoint: path },
    ...params,
  });

  totalRequests.add(1);
  errorRate.add(res.status >= 400);
  slowRequests.add(res.timings.duration > 500);

  return res;
}

/** POST wrapper with JSON body */
export function apiPost(path, body, params = {}) {
  const res = http.post(`${BASE_URL}${path}`, JSON.stringify(body), {
    headers: DEFAULT_HEADERS,
    tags: { endpoint: path },
    ...params,
  });

  totalRequests.add(1);
  errorRate.add(res.status >= 400);
  slowRequests.add(res.timings.duration > 500);

  return res;
}

/** Perform login and return token */
export function login(username = 'user', password = 'user123') {
  const res = apiPost('/login', { username, password });
  loginDuration.add(res.timings.duration);

  check(res, {
    'login: status 200': (r) => r.status === 200,
    'login: has token':  (r) => {
      try { return !!JSON.parse(r.body).token; } catch { return false; }
    },
  });

  if (res.status === 200) {
    try { return JSON.parse(res.body).token; } catch { return null; }
  }
  return null;
}

/** Get products list */
export function getProducts() {
  const res = apiGet('/products');
  productsDuration.add(res.timings.duration);

  check(res, {
    'products: status 200':   (r) => r.status === 200,
    'products: returns array': (r) => {
      try { return Array.isArray(JSON.parse(r.body)); } catch { return false; }
    },
  });

  return res;
}

/** Hit the slow endpoint */
export function hitSlowEndpoint(delayMs = 1000) {
  const res = apiGet(`/lab/slow?delayMs=${delayMs}`);
  dbDuration.add(res.timings.duration);

  check(res, {
    'slow: status 200':   (r) => r.status === 200,
    'slow: within limit': (r) => r.timings.duration < delayMs + 1000,
  });

  return res;
}

/** Standard sleep with jitter to avoid thundering herd */
export function thinkTime(baseMs = 1000) {
  sleep((baseMs + Math.random() * baseMs) / 1000);
}

/** Log a scenario summary to console */
export function logSummary(data, label = 'Test') {
  console.log(`\n=== ${label} Summary ===`);
  console.log(`Requests:      ${data.metrics.http_reqs?.values?.count ?? 0}`);
  console.log(`RPS:           ${data.metrics.http_reqs?.values?.rate?.toFixed(2) ?? 0}`);
  console.log(`Error rate:    ${((data.metrics.http_req_failed?.values?.rate ?? 0) * 100).toFixed(2)}%`);
  console.log(`P50 latency:   ${data.metrics.http_req_duration?.values['p(50)']?.toFixed(0) ?? 0}ms`);
  console.log(`P95 latency:   ${data.metrics.http_req_duration?.values['p(95)']?.toFixed(0) ?? 0}ms`);
  console.log(`P99 latency:   ${data.metrics.http_req_duration?.values['p(99)']?.toFixed(0) ?? 0}ms`);
}
