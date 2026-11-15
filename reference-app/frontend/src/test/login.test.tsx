import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { authRoutes, mockApi, renderApp, USERS } from './helpers';

describe('login page', () => {
  it('redirects unauthenticated visitors to /login', async () => {
    mockApi({});
    renderApp('/incidents');
    expect(await screen.findByTestId('login-submit')).toBeInTheDocument();
  });

  it('shows login-error after invalid credentials and stays on the page', async () => {
    mockApi({
      'POST /auth/login': () => ({ status: 401, body: { code: 'INVALID_CREDENTIALS', message: 'Invalid username or password.', status: 401, traceId: 't' } }),
    });
    renderApp('/login');
    expect(screen.queryByTestId('login-error')).not.toBeInTheDocument();
    await userEvent.type(screen.getByTestId('login-username'), 'admin');
    await userEvent.type(screen.getByTestId('login-password'), 'wrong');
    await userEvent.click(screen.getByTestId('login-submit'));
    expect(await screen.findByTestId('login-error')).toBeInTheDocument();
    expect(localStorage.getItem('s2q.token')).toBeNull();
  });

  it('stores the token under s2q.token and lands on the incident list', async () => {
    const admin = USERS[0];
    mockApi({
      ...authRoutes(admin),
      'POST /auth/login': () => ({ token: 'abc', user: admin }),
      'GET /incidents': () => ({ items: [], page: 1, pageSize: 10, totalItems: 0, totalPages: 0 }),
    });
    renderApp('/login');
    await userEvent.type(screen.getByTestId('login-username'), 'admin');
    await userEvent.type(screen.getByTestId('login-password'), 'S2Q-Demo-2026!');
    await userEvent.click(screen.getByTestId('login-submit'));
    expect(await screen.findByTestId('incident-list')).toBeInTheDocument();
    expect(localStorage.getItem('s2q.token')).toBe('abc');
    expect(screen.getByTestId('current-user')).toHaveTextContent('Ada Admin');
    expect(screen.getByTestId('current-role')).toHaveTextContent('ADMIN');
  });

  it('a 401 from the API clears the session and returns to /login', async () => {
    localStorage.setItem('s2q.token', 'expired');
    mockApi({
      'GET /auth/me': () => ({ status: 401, body: { code: 'UNAUTHENTICATED', message: 'x', status: 401, traceId: 't' } }),
    });
    renderApp('/incidents');
    await waitFor(() => expect(screen.getByTestId('login-submit')).toBeInTheDocument());
    expect(localStorage.getItem('s2q.token')).toBeNull();
  });
});
