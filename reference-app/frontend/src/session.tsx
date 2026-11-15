import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiError, TOKEN_KEY, get, post, setUnauthorizedHandler } from './api';
import type { User } from './types';

interface Session {
  user: User | null;
  loading: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
  error: string | null;
  reportError: (e: unknown) => void;
  clearError: () => void;
  toast: string | null;
  showToast: (message: string) => void;
}

const Ctx = createContext<Session | null>(null);

export function useSession(): Session {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error('SessionProvider missing');
  return ctx;
}

export function SessionProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState<boolean>(!!localStorage.getItem(TOKEN_KEY));
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);

  const clearSession = useCallback(() => {
    localStorage.removeItem(TOKEN_KEY);
    setUser(null);
    setLoading(false);
    navigate('/login');
  }, [navigate]);

  useEffect(() => setUnauthorizedHandler(clearSession), [clearSession]);

  useEffect(() => {
    if (!localStorage.getItem(TOKEN_KEY)) return;
    get<User>('/auth/me')
      .then(setUser)
      .catch(() => undefined)
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 5000);
    return () => clearTimeout(t);
  }, [toast]);

  // Stable callbacks: pages list them as effect dependencies and must not refetch when the toast changes.
  const login = useCallback(async (username: string, password: string) => {
    const res = await post<{ token: string; user: User }>('/auth/login', { username, password });
    localStorage.setItem(TOKEN_KEY, res.token);
    setUser(res.user);
  }, []);
  const reportError = useCallback((e: unknown) => setError(e instanceof ApiError ? e.message : 'Something went wrong.'), []);
  const clearError = useCallback(() => setError(null), []);

  const value = useMemo<Session>(
    () => ({ user, loading, error, toast, login, logout: clearSession, reportError, clearError, showToast: setToast }),
    [user, loading, error, toast, login, clearSession, reportError, clearError],
  );
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}
