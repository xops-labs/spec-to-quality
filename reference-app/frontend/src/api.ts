import type { FieldError } from './types';

export const TOKEN_KEY = 's2q.token';

export class ApiError extends Error {
  constructor(
    public status: number,
    public code: string,
    message: string,
    public errors: FieldError[] = [],
    public details: Record<string, unknown> = {},
  ) {
    super(message);
  }
}

let onUnauthorized: () => void = () => {};

/** Registers what happens on any 401 (the session provider clears the session and redirects). */
export function setUnauthorizedHandler(handler: () => void) {
  onUnauthorized = handler;
}

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export async function api<T>(method: string, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {};
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  const res = await fetch(`/api/v1${path}`, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  if (res.status === 204) return undefined as T;

  let data: any = null;
  try {
    data = await res.json();
  } catch {
    /* non-JSON body */
  }
  if (!res.ok) {
    if (res.status === 401 && path !== '/auth/login') onUnauthorized();
    throw new ApiError(res.status, data?.code ?? 'UNKNOWN', data?.message ?? `Request failed (${res.status})`, data?.errors ?? [], data?.details ?? {});
  }
  return data as T;
}

export const get = <T,>(path: string) => api<T>('GET', path);
export const post = <T,>(path: string, body?: unknown) => api<T>('POST', path, body ?? {});
export const patch = <T,>(path: string, body: unknown) => api<T>('PATCH', path, body);
