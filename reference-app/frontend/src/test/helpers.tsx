import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';
import App from '../App';
import type { Incident, Role, User } from '../types';

export const USERS: User[] = [
  { id: 1, username: 'admin', displayName: 'Ada Admin', role: 'ADMIN', teamId: null },
  { id: 2, username: 'cmd.platform', displayName: 'Casey Platform', role: 'COMMANDER', teamId: 1 },
  { id: 5, username: 'resp.platform1', displayName: 'Robin Platform', role: 'RESPONDER', teamId: 1 },
  { id: 9, username: 'reporter1', displayName: 'Rory Reporter', role: 'REPORTER', teamId: null },
  { id: 10, username: 'viewer1', displayName: 'Val Viewer', role: 'VIEWER', teamId: null },
];

export const SERVICES = [
  { id: 1, key: 'API-GATEWAY', name: 'API Gateway', ownerTeamId: 1, tier: 1, active: true },
  { id: 6, key: 'LEGACY-REPORTS', name: 'Legacy Reports', ownerTeamId: 3, tier: 3, active: false },
];

export function makeIncident(overrides: Partial<Incident> = {}): Incident {
  const t = '2026-03-01T12:00:00.000Z';
  return {
    id: 1, number: 'INC-0001', title: 'Gateway latency spike', description: 'details', serviceId: 1, serviceName: 'API Gateway', teamId: 1,
    severity: 'SEV2', status: 'OPEN', reporterId: 9, assigneeId: null, createdAt: t, updatedAt: t, acknowledgedAt: null, mitigatedAt: null,
    resolvedAt: null, closedAt: null, resolutionSummary: null, escalationLevel: 0, reopenCount: 0, version: 1, actionItemCount: 0, openActionItemCount: 0,
    sla: {
      ack: { targetMinutes: 15, deadline: '2026-03-01T12:15:00.000Z', status: 'RUNNING' },
      resolve: { targetMinutes: 240, deadline: '2026-03-01T16:00:00.000Z', status: 'RUNNING' },
      breached: false,
    },
    ...overrides,
  };
}

type Handler = (url: URL, init: RequestInit) => unknown;

/** Routes fetch('/api/v1/...') calls to handlers keyed by "METHOD /path"; unhandled calls fail the test loudly. */
export function mockApi(routes: Record<string, Handler>) {
  const calls: { method: string; path: string; body: unknown }[] = [];
  const fn = vi.fn(async (input: RequestInfo | URL, init: RequestInit = {}) => {
    const url = new URL(String(input), 'http://localhost');
    const method = init.method ?? 'GET';
    const path = url.pathname.replace('/api/v1', '');
    calls.push({ method, path: path + url.search, body: init.body ? JSON.parse(String(init.body)) : undefined });
    const handler = routes[`${method} ${path}`];
    if (!handler) throw new Error(`Unmocked request: ${method} ${path}${url.search}`);
    const result = handler(url, init) as { status?: unknown; body?: unknown } | undefined;
    // `{ status: <number>, body }` is an explicit HTTP envelope; anything else is the 200 response body itself
    // (an incident has a string `status`, so the type check keeps the two apart).
    const envelope = typeof result?.status === 'number' && result !== undefined && 'body' in result;
    const status = envelope ? (result!.status as number) : 200;
    return new Response(status === 204 ? null : JSON.stringify(envelope ? result!.body : result), {
      status,
      headers: { 'Content-Type': 'application/json' },
    });
  });
  vi.stubGlobal('fetch', fn);
  return { calls, fn };
}

export function signInAs(role: Role) {
  const user = USERS.find((u) => u.role === role)!;
  localStorage.setItem('s2q.token', 'test-token');
  return user;
}

export function renderApp(route: string) {
  return render(
    <MemoryRouter initialEntries={[route]}>
      <App />
    </MemoryRouter>,
  );
}

export const authRoutes = (user: User): Record<string, Handler> => ({
  'GET /auth/me': () => user,
  'GET /users': () => USERS,
  'GET /services': () => SERVICES,
});
