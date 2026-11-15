import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { validate } from '../pages/NewIncident';
import { authRoutes, makeIncident, mockApi, renderApp, signInAs } from './helpers';

describe('validate()', () => {
  it('applies the spec rules to the trimmed values', () => {
    expect(validate('  abcd  ', '', '1', 'SEV1').title).toMatch(/at least 5/);
    expect(validate('abcde', '', '1', 'SEV1')).toEqual({});
    expect(validate('x'.repeat(121), '', '1', 'SEV1').title).toMatch(/at most 120/);
    expect(validate('valid title', 'd'.repeat(4001), '1', 'SEV1').description).toBeDefined();
    expect(validate('valid title', '', '', '')).toEqual({ serviceId: 'Service is required.', severity: 'Severity is required.' });
  });
});

describe('create incident page', () => {
  it('redirects viewers to the list', async () => {
    const viewer = signInAs('VIEWER');
    mockApi({ ...authRoutes(viewer), 'GET /services': () => [], 'GET /incidents': () => ({ items: [], page: 1, pageSize: 10, totalItems: 0, totalPages: 0 }) });
    renderApp('/incidents/new');
    expect(await screen.findByTestId('incident-list')).toBeInTheDocument();
  });

  it('lists only active services behind an empty placeholder option', async () => {
    const user = signInAs('REPORTER');
    mockApi({ ...authRoutes(user) });
    renderApp('/incidents/new');
    const select = await screen.findByTestId('new-service');
    await screen.findByRole('option', { name: 'API Gateway' });
    const values = Array.from((select as HTMLSelectElement).options).map((o) => o.value);
    expect(values).toEqual(['', '1']);
    expect(Array.from((screen.getByTestId('new-severity') as HTMLSelectElement).options).map((o) => o.value)).toEqual(['', 'SEV1', 'SEV2', 'SEV3', 'SEV4']);
  });

  it('shows client-side field errors and does not call the API', async () => {
    const user = signInAs('REPORTER');
    const { calls } = mockApi({ ...authRoutes(user) });
    renderApp('/incidents/new');
    await screen.findByTestId('new-submit');
    await userEvent.type(screen.getByTestId('new-title'), 'abc');
    await userEvent.click(screen.getByTestId('new-submit'));
    expect(screen.getByTestId('error-title')).toBeInTheDocument();
    expect(screen.getByTestId('error-serviceId')).toBeInTheDocument();
    expect(screen.getByTestId('error-severity')).toBeInTheDocument();
    expect(screen.queryByTestId('error-description')).not.toBeInTheDocument();
    expect(calls.some((c) => c.method === 'POST')).toBe(false);
  });

  it('maps server 422 field errors to error-* and business rules to form-error', async () => {
    const user = signInAs('REPORTER');
    let n = 0;
    mockApi({
      ...authRoutes(user),
      'POST /incidents': () => (n++ === 0
        ? { status: 422, body: { code: 'VALIDATION_ERROR', message: 'bad', status: 422, traceId: 't', errors: [{ field: 'serviceId', code: 'not_found', message: 'Service does not exist.' }] } }
        : { status: 409, body: { code: 'DUPLICATE_INCIDENT', message: 'Already exists.', status: 409, traceId: 't', details: { existingIncidentId: 4 } } }),
    });
    renderApp('/incidents/new');
    await screen.findByRole('option', { name: 'API Gateway' });
    await userEvent.type(screen.getByTestId('new-title'), 'Gateway down');
    await userEvent.selectOptions(screen.getByTestId('new-service'), '1');
    await userEvent.selectOptions(screen.getByTestId('new-severity'), 'SEV2');
    await userEvent.click(screen.getByTestId('new-submit'));
    expect(await screen.findByTestId('error-serviceId')).toHaveTextContent('Service does not exist.');
    await userEvent.click(screen.getByTestId('new-submit'));
    expect(await screen.findByTestId('form-error')).toHaveTextContent('Already exists.');
    expect(screen.queryByTestId('error-serviceId')).not.toBeInTheDocument();
  });

  it('navigates to the new incident on success', async () => {
    const user = signInAs('REPORTER');
    const { calls } = mockApi({
      ...authRoutes(user),
      'POST /incidents': () => ({ status: 201, body: makeIncident({ id: 7, number: 'INC-0007' }) }),
      'GET /incidents/7': () => makeIncident({ id: 7, number: 'INC-0007' }),
      'GET /incidents/7/notes': () => [],
      'GET /incidents/7/action-items': () => [],
    });
    renderApp('/incidents/new');
    await screen.findByRole('option', { name: 'API Gateway' });
    await userEvent.type(screen.getByTestId('new-title'), 'Gateway down');
    await userEvent.selectOptions(screen.getByTestId('new-service'), '1');
    await userEvent.selectOptions(screen.getByTestId('new-severity'), 'SEV2');
    await userEvent.click(screen.getByTestId('new-submit'));
    expect(await screen.findByTestId('detail-number')).toHaveTextContent('INC-0007');
    expect(calls.find((c) => c.method === 'POST')!.body).toEqual({ title: 'Gateway down', description: '', serviceId: 1, severity: 'SEV2' });
  });
});
