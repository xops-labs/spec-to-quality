import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { get } from '../api';
import { useSession } from '../session';
import { SEVERITIES, STATUSES, type Incident, type Page, type Service, type User } from '../types';

interface Filters {
  status: string;
  severity: string;
  serviceId: string;
  q: string;
  slaBreached: boolean;
}

const EMPTY: Filters = { status: '', severity: '', serviceId: '', q: '', slaBreached: false };
const PAGE_SIZE = 10;

export const slaLabel = (i: Incident): 'BREACHED' | 'AT_RISK' | 'OK' =>
  i.sla.breached ? 'BREACHED' : i.sla.ack.status === 'AT_RISK' || i.sla.resolve.status === 'AT_RISK' ? 'AT_RISK' : 'OK';

function buildQuery(f: Filters, page: number): string {
  const p = new URLSearchParams({ sort: 'createdAt', order: 'desc', pageSize: String(PAGE_SIZE), page: String(page) });
  if (f.status) p.set('status', f.status);
  if (f.severity) p.set('severity', f.severity);
  if (f.serviceId) p.set('serviceId', f.serviceId);
  if (f.q.trim()) p.set('q', f.q.trim());
  if (f.slaBreached) p.set('slaBreached', 'true');
  return p.toString();
}

export default function IncidentList() {
  const { user, reportError } = useSession();
  const navigate = useNavigate();
  const [draft, setDraft] = useState<Filters>(EMPTY); // what the controls show
  const [applied, setApplied] = useState<Filters>(EMPTY); // what the last "apply" committed
  const [page, setPage] = useState(1);
  const [data, setData] = useState<Page<Incident> | null>(null);
  const [services, setServices] = useState<Service[]>([]);
  const [users, setUsers] = useState<Map<number, User>>(new Map());
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    get<Service[]>('/services').then(setServices).catch(reportError);
    get<User[]>('/users').then((u) => setUsers(new Map(u.map((x) => [x.id, x])))).catch(reportError);
  }, [reportError]);

  const load = useCallback(
    async (f: Filters, p: number) => {
      setLoading(true);
      try {
        setData(await get<Page<Incident>>(`/incidents?${buildQuery(f, p)}`));
      } catch (e) {
        reportError(e);
      } finally {
        setLoading(false);
      }
    },
    [reportError],
  );

  useEffect(() => {
    void load(applied, page);
  }, [load, applied, page]);

  const apply = () => {
    setPage(1);
    setApplied(draft);
  };
  const reset = () => {
    setDraft(EMPTY);
    setPage(1);
    setApplied(EMPTY);
  };

  const items = data?.items ?? [];
  const totalPages = data && data.totalItems > 0 ? data.totalPages : 1;

  return (
    <div data-testid="incident-list">
      <div className="row">
        <h2 style={{ flex: 1 }}>Incidents</h2>
        {user && user.role !== 'VIEWER' && (
          <Link data-testid="new-incident-link" to="/incidents/new">New incident</Link>
        )}
      </div>

      <div className="card row">
        <select data-testid="filter-status" value={draft.status} onChange={(e) => setDraft({ ...draft, status: e.target.value })}>
          <option value="">All statuses</option>
          {STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
        <select data-testid="filter-severity" value={draft.severity} onChange={(e) => setDraft({ ...draft, severity: e.target.value })}>
          <option value="">All severities</option>
          {SEVERITIES.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
        <select data-testid="filter-service" value={draft.serviceId} onChange={(e) => setDraft({ ...draft, serviceId: e.target.value })}>
          <option value="">All services</option>
          {services.map((s) => <option key={s.id} value={String(s.id)}>{s.name}</option>)}
        </select>
        <input data-testid="filter-search" placeholder="Search title or number" value={draft.q} onChange={(e) => setDraft({ ...draft, q: e.target.value })} />
        <label style={{ margin: 0 }}>
          <input data-testid="filter-sla-breached" type="checkbox" checked={draft.slaBreached} onChange={(e) => setDraft({ ...draft, slaBreached: e.target.checked })} />
          SLA breached
        </label>
        <button data-testid="filter-apply" onClick={apply}>Apply</button>
        <button data-testid="filter-reset" onClick={reset}>Reset</button>
      </div>

      {loading && <p data-testid="list-loading">Loading…</p>}
      {!loading && items.length === 0 && <p data-testid="list-empty">No incidents match.</p>}

      {items.length > 0 && (
        <table>
          <thead>
            <tr><th>Number</th><th>Title</th><th>Severity</th><th>Status</th><th>Assignee</th><th>SLA</th><th>Escalation</th></tr>
          </thead>
          <tbody>
            {items.map((i) => (
              <tr key={i.id} data-testid="incident-row" data-incident-id={i.id}>
                <td data-testid="incident-number">{i.number}</td>
                <td>
                  <a data-testid="incident-title" href={`/incidents/${i.id}`} onClick={(e) => { e.preventDefault(); navigate(`/incidents/${i.id}`); }}>{i.title}</a>
                </td>
                <td data-testid="incident-severity">{i.severity}</td>
                <td data-testid="incident-status">{i.status}</td>
                <td data-testid="incident-assignee">{i.assigneeId === null ? 'Unassigned' : users.get(i.assigneeId)?.displayName ?? `User ${i.assigneeId}`}</td>
                <td data-testid="incident-sla">{slaLabel(i)}</td>
                <td data-testid="incident-escalation">{i.escalationLevel}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <div className="row">
        <button data-testid="pagination-prev" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</button>
        <span data-testid="pagination-info">{`Page ${data && data.totalItems > 0 ? data.page : 1} of ${totalPages}`}</span>
        <button data-testid="pagination-next" disabled={!data || data.totalItems === 0 || page >= data.totalPages} onClick={() => setPage(page + 1)}>Next</button>
      </div>
    </div>
  );
}
