import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { authRoutes, makeIncident, mockApi, renderApp, signInAs } from './helpers';

const page = (items: ReturnType<typeof makeIncident>[], p = 1, totalPages = 1) => ({ items, page: p, pageSize: 10, totalItems: items.length, totalPages });

describe('incident list', () => {
  it('renders one row per incident with the contract test ids and SLA label', async () => {
    const user = signInAs('VIEWER');
    const breached = makeIncident({ id: 2, number: 'INC-0002', title: 'Second', severity: 'SEV1', assigneeId: 5, escalationLevel: 1, sla: { ...makeIncident().sla, breached: true } });
    const atRisk = makeIncident({ id: 3, number: 'INC-0003', title: 'Third', sla: { ...makeIncident().sla, ack: { ...makeIncident().sla.ack, status: 'AT_RISK' } } });
    mockApi({ ...authRoutes(user), 'GET /incidents': () => page([breached, atRisk, makeIncident()]) });
    renderApp('/incidents');

    const rows = await screen.findAllByTestId('incident-row');
    expect(rows).toHaveLength(3);
    expect(rows[0]).toHaveAttribute('data-incident-id', '2');
    expect(within(rows[0]).getByTestId('incident-number')).toHaveTextContent('INC-0002');
    expect(within(rows[0]).getByTestId('incident-severity')).toHaveTextContent('SEV1');
    expect(within(rows[0]).getByTestId('incident-assignee')).toHaveTextContent('Robin Platform');
    expect(within(rows[0]).getByTestId('incident-sla')).toHaveTextContent('BREACHED');
    expect(within(rows[0]).getByTestId('incident-escalation')).toHaveTextContent('1');
    expect(within(rows[1]).getByTestId('incident-sla')).toHaveTextContent('AT_RISK');
    expect(within(rows[2]).getByTestId('incident-sla')).toHaveTextContent('OK');
    expect(within(rows[2]).getByTestId('incident-assignee')).toHaveTextContent('Unassigned');
    expect(screen.queryByTestId('list-empty')).not.toBeInTheDocument();
  });

  it('hides the new-incident entry points from viewers and shows them to reporters', async () => {
    const viewer = signInAs('VIEWER');
    mockApi({ ...authRoutes(viewer), 'GET /incidents': () => page([]) });
    const { unmount } = renderApp('/incidents');
    await screen.findByTestId('incident-list');
    expect(screen.queryByTestId('nav-new-incident')).not.toBeInTheDocument();
    expect(screen.queryByTestId('new-incident-link')).not.toBeInTheDocument();
    unmount();

    const reporter = signInAs('REPORTER');
    mockApi({ ...authRoutes(reporter), 'GET /incidents': () => page([]) });
    renderApp('/incidents');
    expect(await screen.findByTestId('nav-new-incident')).toBeInTheDocument();
    expect(screen.getByTestId('new-incident-link')).toBeInTheDocument();
  });

  it('shows list-empty and "Page 1 of 1" with no results, with pagination disabled', async () => {
    const user = signInAs('ADMIN');
    mockApi({ ...authRoutes(user), 'GET /incidents': () => page([], 1, 0) });
    renderApp('/incidents');
    expect(await screen.findByTestId('list-empty')).toBeInTheDocument();
    expect(screen.getByTestId('pagination-info')).toHaveTextContent(/^Page 1 of 1$/);
    expect(screen.getByTestId('pagination-prev')).toBeDisabled();
    expect(screen.getByTestId('pagination-next')).toBeDisabled();
  });

  it('requests the default sort and applies filters only when "apply" is clicked', async () => {
    const user = signInAs('ADMIN');
    const { calls } = mockApi({ ...authRoutes(user), 'GET /incidents': () => page([makeIncident()], 1, 3) });
    renderApp('/incidents');
    await screen.findAllByTestId('incident-row');
    const first = calls.filter((c) => c.path.startsWith('/incidents?'));
    expect(first).toHaveLength(1);
    expect(first[0].path).toContain('sort=createdAt');
    expect(first[0].path).toContain('order=desc');
    expect(first[0].path).toContain('pageSize=10');

    await userEvent.selectOptions(screen.getByTestId('filter-severity'), 'SEV1');
    await userEvent.type(screen.getByTestId('filter-search'), 'gate');
    await userEvent.click(screen.getByTestId('filter-sla-breached'));
    expect(calls.filter((c) => c.path.startsWith('/incidents?'))).toHaveLength(1); // nothing yet

    await userEvent.click(screen.getByTestId('filter-apply'));
    await screen.findAllByTestId('incident-row');
    const last = calls.filter((c) => c.path.startsWith('/incidents?')).at(-1)!;
    expect(last.path).toContain('severity=SEV1');
    expect(last.path).toContain('q=gate');
    expect(last.path).toContain('slaBreached=true');

    await userEvent.click(screen.getByTestId('filter-reset'));
    await screen.findAllByTestId('incident-row');
    const afterReset = calls.filter((c) => c.path.startsWith('/incidents?')).at(-1)!;
    expect(afterReset.path).not.toContain('severity=');
    expect(screen.getByTestId('filter-severity')).toHaveValue('');
  });

  it('paginates with prev/next and reports the page', async () => {
    const user = signInAs('ADMIN');
    const { calls } = mockApi({
      ...authRoutes(user),
      'GET /incidents': (url) => page([makeIncident()], Number(url.searchParams.get('page')), 2),
    });
    renderApp('/incidents');
    await screen.findAllByTestId('incident-row');
    expect(screen.getByTestId('pagination-info')).toHaveTextContent('Page 1 of 2');
    expect(screen.getByTestId('pagination-prev')).toBeDisabled();
    expect(screen.getByTestId('pagination-next')).toBeEnabled();
    await userEvent.click(screen.getByTestId('pagination-next'));
    await screen.findByText('Page 2 of 2');
    expect(screen.getByTestId('pagination-next')).toBeDisabled();
    expect(calls.at(-1)!.path).toContain('page=2');
  });

  it('navigates to the detail page when the title is clicked', async () => {
    const user = signInAs('ADMIN');
    mockApi({
      ...authRoutes(user),
      'GET /incidents': () => page([makeIncident()]),
      'GET /incidents/1': () => makeIncident(),
      'GET /incidents/1/notes': () => [],
      'GET /incidents/1/action-items': () => [],
      'GET /incidents/1/audit': () => [],
    });
    renderApp('/incidents');
    await userEvent.click(await screen.findByTestId('incident-title'));
    expect(await screen.findByTestId('detail-number')).toHaveTextContent('INC-0001');
  });
});
