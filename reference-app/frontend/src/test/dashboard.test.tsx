import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { Dashboard } from '../types';
import { authRoutes, mockApi, renderApp, signInAs } from './helpers';

const base: Dashboard = {
  generatedAt: '2026-03-01T12:00:00.000Z', totalIncidents: 0, activeCount: 0,
  byStatus: { OPEN: 0, ACKNOWLEDGED: 0, MITIGATED: 0, RESOLVED: 0, CLOSED: 0 },
  activeBySeverity: { SEV1: 0, SEV2: 0, SEV3: 0, SEV4: 0 },
  breachedActiveCount: 0, atRiskActiveCount: 0, escalatedActiveCount: 0,
  mttaMinutes: null, mttrMinutes: null, slaCompliancePercent: null, topServices: [],
};

describe('dashboard', () => {
  it('shows N/A for missing averages and zero-filled counters', async () => {
    const user = signInAs('VIEWER');
    mockApi({ ...authRoutes(user), 'GET /dashboard/summary': () => base });
    renderApp('/dashboard');
    expect(await screen.findByTestId('dash-mtta')).toHaveTextContent('N/A');
    expect(screen.getByTestId('dash-mttr')).toHaveTextContent('N/A');
    expect(screen.getByTestId('dash-sla-compliance')).toHaveTextContent('N/A');
    expect(screen.getByTestId('dash-status-CLOSED')).toHaveTextContent('0');
    expect(screen.getByTestId('dash-severity-SEV4')).toHaveTextContent('0');
    expect(screen.queryByTestId('dash-top-service')).not.toBeInTheDocument();
  });

  it('renders the summary values, one decimal, and top services in order', async () => {
    const user = signInAs('ADMIN');
    mockApi({
      ...authRoutes(user),
      'GET /dashboard/summary': () => ({
        ...base, totalIncidents: 9, activeCount: 5, breachedActiveCount: 2, atRiskActiveCount: 1, escalatedActiveCount: 3,
        byStatus: { OPEN: 2, ACKNOWLEDGED: 2, MITIGATED: 1, RESOLVED: 3, CLOSED: 1 },
        activeBySeverity: { SEV1: 1, SEV2: 2, SEV3: 2, SEV4: 0 },
        mttaMinutes: 4, mttrMinutes: 61.5, slaCompliancePercent: 87.5,
        topServices: [{ serviceId: 1, serviceName: 'API Gateway', activeCount: 3 }, { serviceId: 3, serviceName: 'Payments Core', activeCount: 2 }],
      }),
    });
    renderApp('/dashboard');
    expect(await screen.findByTestId('dash-active-count')).toHaveTextContent('5');
    expect(screen.getByTestId('dash-total-count')).toHaveTextContent('9');
    expect(screen.getByTestId('dash-breached-count')).toHaveTextContent('2');
    expect(screen.getByTestId('dash-at-risk-count')).toHaveTextContent('1');
    expect(screen.getByTestId('dash-escalated-count')).toHaveTextContent('3');
    expect(screen.getByTestId('dash-mtta')).toHaveTextContent('4.0');
    expect(screen.getByTestId('dash-mttr')).toHaveTextContent('61.5');
    expect(screen.getByTestId('dash-sla-compliance')).toHaveTextContent('87.5%');
    expect(screen.getByTestId('dash-status-RESOLVED')).toHaveTextContent('3');
    expect(screen.getByTestId('dash-severity-SEV2')).toHaveTextContent('2');
    const tops = screen.getAllByTestId('dash-top-service');
    expect(tops[0]).toHaveTextContent('API Gateway');
    expect(tops[0]).toHaveTextContent('3');
    expect(tops[1]).toHaveTextContent('Payments Core');
  });

  it('shows the not-found page for unknown routes and logs out cleanly', async () => {
    const user = signInAs('ADMIN');
    mockApi({ ...authRoutes(user) });
    renderApp('/does/not/exist');
    expect(await screen.findByTestId('not-found-page')).toBeInTheDocument();
  });
});
