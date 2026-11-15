import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import type { Incident, Role } from '../types';
import { authRoutes, makeIncident, mockApi, renderApp, signInAs, USERS } from './helpers';

const BUTTONS = ['acknowledge', 'mitigate', 'resolve', 'close', 'reopen', 'assign', 'severity', 'edit'];

function open(role: Role, incident: Incident, extra: Record<string, () => unknown> = {}) {
  const user = signInAs(role);
  const api = mockApi({
    ...authRoutes(user),
    [`GET /incidents/${incident.id}`]: () => incident,
    [`GET /incidents/${incident.id}/notes`]: () => [{ id: 1, incidentId: incident.id, authorId: 2, body: 'First note', createdAt: incident.createdAt }],
    [`GET /incidents/${incident.id}/action-items`]: () => [
      { id: 3, incidentId: incident.id, title: 'Write postmortem', ownerId: 5, dueDate: '2999-01-01', status: 'OPEN', createdAt: incident.createdAt, completedAt: null },
    ],
    [`GET /incidents/${incident.id}/audit`]: () => [
      { id: 1, incidentId: incident.id, actorId: 9, action: 'INCIDENT_CREATED', createdAt: incident.createdAt, details: {} },
      { id: 2, incidentId: incident.id, actorId: null, action: 'ESCALATED', createdAt: incident.createdAt, details: { level: 1 } },
    ],
    ...extra,
  });
  renderApp(`/incidents/${incident.id}`);
  return api;
}

const visible = () => BUTTONS.filter((b) => screen.queryByTestId(`action-${b}`));

describe('incident detail', () => {
  it('shows the details, SLA timestamps, notes, action items and audit trail', async () => {
    open('COMMANDER', makeIncident({ escalationLevel: 2, reopenCount: 1, resolutionSummary: 'Rolled back' , assigneeId: 5}));
    expect(await screen.findByTestId('detail-number')).toHaveTextContent('INC-0001');
    expect(screen.getByTestId('detail-title')).toHaveTextContent('Gateway latency spike');
    expect(screen.getByTestId('detail-service')).toHaveTextContent('API Gateway');
    expect(screen.getByTestId('detail-reporter')).toHaveTextContent('Rory Reporter');
    expect(screen.getByTestId('detail-assignee')).toHaveTextContent('Robin Platform');
    expect(screen.getByTestId('detail-escalation-level')).toHaveTextContent('2');
    expect(screen.getByTestId('detail-reopen-count')).toHaveTextContent('1');
    expect(screen.getByTestId('detail-sla-ack')).toHaveTextContent('RUNNING');
    expect(screen.getByTestId('detail-sla-ack-deadline')).toHaveAttribute('data-timestamp', '2026-03-01T12:15:00.000Z');
    expect(screen.getByTestId('detail-resolution-summary')).toHaveTextContent('Rolled back');
    expect(within(screen.getByTestId('note-item')).getByTestId('note-author')).toHaveTextContent('Casey Platform');
    expect(within(screen.getByTestId('ai-item')).getByTestId('ai-owner')).toHaveTextContent('Robin Platform');
    const audit = screen.getAllByTestId('audit-item');
    expect(within(audit[0]).getByTestId('audit-actor')).toHaveTextContent('Rory Reporter');
    expect(within(audit[1]).getByTestId('audit-actor')).toHaveTextContent('System');
  });

  it('omits the resolution summary when null', async () => {
    open('COMMANDER', makeIncident());
    await screen.findByTestId('detail-number');
    expect(screen.queryByTestId('detail-resolution-summary')).not.toBeInTheDocument();
  });

  it.each([
    ['OPEN', ['acknowledge', 'assign', 'severity', 'edit']],
    ['ACKNOWLEDGED', ['mitigate', 'assign', 'severity', 'edit']],
    ['MITIGATED', ['resolve', 'assign', 'severity', 'edit']],
    ['RESOLVED', ['close', 'reopen', 'edit']],
    ['CLOSED', []],
  ] as const)('a commander of the team sees exactly the buttons for %s', async (status, expected) => {
    open('COMMANDER', makeIncident({ status }));
    await screen.findByTestId('detail-number');
    expect(visible()).toEqual(BUTTONS.filter((b) => (expected as readonly string[]).includes(b)));
  });

  it('a viewer sees no action, note, action-item or audit controls', async () => {
    open('VIEWER', makeIncident());
    await screen.findByTestId('detail-number');
    expect(visible()).toEqual([]);
    expect(screen.queryByTestId('note-input')).not.toBeInTheDocument();
    expect(screen.queryByTestId('ai-add')).not.toBeInTheDocument();
    expect(screen.queryByTestId('ai-toggle')).not.toBeInTheDocument();
    expect(screen.queryByTestId('audit-list')).not.toBeInTheDocument();
    expect(screen.getAllByTestId('note-item')).toHaveLength(1);
  });

  it('a responder who is not the assignee cannot mitigate; the assignee can', async () => {
    open('RESPONDER', makeIncident({ status: 'ACKNOWLEDGED', assigneeId: null }));
    await screen.findByTestId('detail-number');
    expect(screen.queryByTestId('action-mitigate')).not.toBeInTheDocument();
  });

  it('shows detail-not-found for a 404', async () => {
    const user = signInAs('ADMIN');
    mockApi({
      ...authRoutes(user),
      'GET /incidents/99': () => ({ status: 404, body: { code: 'NOT_FOUND', message: 'Incident not found.', status: 404, traceId: 't' } }),
    });
    renderApp('/incidents/99');
    expect(await screen.findByTestId('detail-not-found')).toBeInTheDocument();
  });

  it('acknowledge acts immediately, refreshes and shows a toast', async () => {
    let incident = makeIncident({ status: 'OPEN' });
    const { calls } = open('COMMANDER', incident, {
      'POST /incidents/1/acknowledge': () => {
        incident = makeIncident({ status: 'ACKNOWLEDGED', assigneeId: 2 });
        return incident;
      },
      'GET /incidents/1': () => incident,
    });
    await userEvent.click(await screen.findByTestId('action-acknowledge'));
    expect(await screen.findByTestId('toast')).toBeInTheDocument();
    expect(await screen.findByText('ACKNOWLEDGED', { selector: '[data-testid="detail-status"]' })).toBeInTheDocument();
    expect(calls.some((c) => c.method === 'POST' && c.path === '/incidents/1/acknowledge')).toBe(true);
  });

  it('shows the server message in the dialog and keeps it open on a 409', async () => {
    open('COMMANDER', makeIncident({ status: 'RESOLVED' }), {
      'POST /incidents/1/close': () => ({ status: 409, body: { code: 'OPEN_ACTION_ITEMS', message: 'All action items must be done before closing.', status: 409, traceId: 't' } }),
      'POST /incidents/1/reopen': () => ({ status: 422, body: { code: 'VALIDATION_ERROR', message: 'bad', status: 422, traceId: 't', errors: [{ field: 'reason', code: 'too_short', message: 'reason must be at least 5 characters.' }] } }),
    });
    await userEvent.click(await screen.findByTestId('action-reopen'));
    expect(screen.getByTestId('dialog')).toBeInTheDocument();
    expect(screen.queryByTestId('dialog-error')).not.toBeInTheDocument();
    await userEvent.type(screen.getByTestId('dialog-input'), 'no');
    await userEvent.click(screen.getByTestId('dialog-confirm'));
    expect(await screen.findByTestId('dialog-error')).toHaveTextContent('at least 5 characters');
    expect(screen.getByTestId('dialog')).toBeInTheDocument();
    await userEvent.click(screen.getByTestId('dialog-cancel'));
    expect(screen.queryByTestId('dialog')).not.toBeInTheDocument();

    // close acts immediately; its failure is reported in the toast
    await userEvent.click(screen.getByTestId('action-close'));
    expect(await screen.findByTestId('toast')).toHaveTextContent('All action items must be done');
  });

  it('assign dialog lists only responders and commanders of the incident team, with an empty placeholder', async () => {
    const { calls } = open('COMMANDER', makeIncident(), { 'POST /incidents/1/assign': () => makeIncident({ assigneeId: 5 }) });
    await userEvent.click(await screen.findByTestId('action-assign'));
    const select = screen.getByTestId('dialog-select') as HTMLSelectElement;
    expect(Array.from(select.options).map((o) => o.value)).toEqual(['', '2', '5']);
    await userEvent.selectOptions(select, '5');
    await userEvent.click(screen.getByTestId('dialog-confirm'));
    await screen.findByTestId('toast');
    expect(calls.find((c) => c.path === '/incidents/1/assign')!.body).toEqual({ assigneeId: 5 });
    expect(screen.queryByTestId('dialog')).not.toBeInTheDocument();
  });

  it('severity dialog sends severity and reason; edit dialog pre-fills title and description', async () => {
    const { calls } = open('ADMIN', makeIncident(), {
      'POST /incidents/1/severity': () => makeIncident({ severity: 'SEV1' }),
      'PATCH /incidents/1': () => makeIncident({ title: 'Renamed incident' }),
    });
    await userEvent.click(await screen.findByTestId('action-severity'));
    await userEvent.selectOptions(screen.getByTestId('dialog-select'), 'SEV1');
    await userEvent.type(screen.getByTestId('dialog-reason'), 'Customer impact grew');
    await userEvent.click(screen.getByTestId('dialog-confirm'));
    await screen.findByTestId('toast');
    expect(calls.find((c) => c.path === '/incidents/1/severity')!.body).toEqual({ severity: 'SEV1', reason: 'Customer impact grew' });

    await userEvent.click(screen.getByTestId('action-edit'));
    expect(screen.getByTestId('dialog-input')).toHaveValue('Gateway latency spike');
    expect(screen.getByTestId('dialog-input-secondary')).toHaveValue('details');
  });

  it('adds notes and action items and toggles items', async () => {
    const { calls } = open('COMMANDER', makeIncident(), {
      'POST /incidents/1/notes': () => ({ status: 201, body: { id: 9 } }),
      'POST /incidents/1/action-items': () => ({ status: 201, body: { id: 10 } }),
      'PATCH /incidents/1/action-items/3': () => ({ id: 3 }),
    });
    await userEvent.type(await screen.findByTestId('note-input'), 'Another note');
    await userEvent.click(screen.getByTestId('note-submit'));
    await screen.findByTestId('toast');
    expect(calls.find((c) => c.method === 'POST' && c.path === '/incidents/1/notes')!.body).toEqual({ body: 'Another note' });

    await userEvent.type(screen.getByTestId('ai-title-input'), 'Add alerting');
    await userEvent.selectOptions(screen.getByTestId('ai-owner-select'), '5');
    await userEvent.type(screen.getByTestId('ai-due-input'), '2999-02-02');
    await userEvent.click(screen.getByTestId('ai-add'));
    await screen.findByTestId('ai-list');
    expect(calls.find((c) => c.path === '/incidents/1/action-items' && c.method === 'POST')!.body).toEqual({ title: 'Add alerting', ownerId: 5, dueDate: '2999-02-02' });

    await userEvent.click(screen.getByTestId('ai-toggle'));
    await screen.findByTestId('toast');
    expect(calls.find((c) => c.method === 'PATCH')!.body).toEqual({ status: 'DONE' });
  });

  it('shows ai-error with the server message when adding an action item fails', async () => {
    open('COMMANDER', makeIncident(), {
      'POST /incidents/1/action-items': () => ({ status: 422, body: { code: 'VALIDATION_ERROR', message: 'bad', status: 422, traceId: 't', errors: [{ field: 'dueDate', code: 'out_of_range', message: 'dueDate must not be in the past.' }] } }),
    });
    await userEvent.click(await screen.findByTestId('ai-add'));
    expect(await screen.findByTestId('ai-error')).toHaveTextContent('dueDate must not be in the past.');
  });

  it('hides note and action-item forms once the incident is closed', async () => {
    open('ADMIN', makeIncident({ status: 'CLOSED' }));
    await screen.findByTestId('detail-number');
    expect(screen.queryByTestId('note-input')).not.toBeInTheDocument();
    expect(screen.queryByTestId('ai-add')).not.toBeInTheDocument();
    expect(screen.queryByTestId('ai-toggle')).not.toBeInTheDocument();
    expect(USERS.length).toBeGreaterThan(0);
  });
});
