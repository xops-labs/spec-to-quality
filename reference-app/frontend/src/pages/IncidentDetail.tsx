import { useCallback, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { ApiError, get, patch, post } from '../api';
import Dialog from '../components/Dialog';
import { can, canAct, type Op } from '../permissions';
import { useSession } from '../session';
import { SEVERITIES, type ActionItem, type AuditEvent, type Incident, type Note, type User } from '../types';

type DialogKind = 'mitigate' | 'resolve' | 'reopen' | 'assign' | 'severity' | 'edit';

const TITLES: Record<DialogKind, string> = {
  mitigate: 'Mitigate incident', resolve: 'Resolve incident', reopen: 'Reopen incident',
  assign: 'Assign incident', severity: 'Change severity', edit: 'Edit incident',
};

export default function IncidentDetail() {
  const { id } = useParams();
  const { user, reportError, showToast } = useSession();
  const [incident, setIncident] = useState<Incident | null>(null);
  const [notFound, setNotFound] = useState(false);
  const [users, setUsers] = useState<User[]>([]);
  const [notes, setNotes] = useState<Note[]>([]);
  const [items, setItems] = useState<ActionItem[]>([]);
  const [audit, setAudit] = useState<AuditEvent[] | null>(null);
  const [dialog, setDialog] = useState<DialogKind | null>(null);

  const load = useCallback(async () => {
    try {
      const inc = await get<Incident>(`/incidents/${id}`);
      setIncident(inc);
      setNotFound(false);
      const [n, a] = await Promise.all([get<Note[]>(`/incidents/${id}/notes`), get<ActionItem[]>(`/incidents/${id}/action-items`)]);
      setNotes(n);
      setItems(a);
      if (user && can(user, 'readAudit', inc)) setAudit(await get<AuditEvent[]>(`/incidents/${id}/audit`));
      else setAudit(null);
    } catch (e) {
      if (e instanceof ApiError && e.status === 404) setNotFound(true);
      else reportError(e);
    }
  }, [id, user, reportError]);

  useEffect(() => {
    get<User[]>('/users').then(setUsers).catch(reportError);
  }, [reportError]);

  useEffect(() => {
    void load();
  }, [load]);

  if (notFound) return <div data-testid="detail-not-found">Incident not found.</div>;
  if (!incident || !user) return <p>Loading…</p>;

  const name = (uid: number | null) => (uid === null ? null : users.find((u) => u.id === uid)?.displayName ?? `User ${uid}`);
  const run = async (action: () => Promise<unknown>, success: string) => {
    try {
      await action();
      showToast(success);
      await load();
    } catch (e) {
      // acknowledge/close have no dialog, so their failures are shown as a toast with the server message
      showToast(e instanceof ApiError ? e.message : 'Something went wrong.');
    }
  };
  const base = `/incidents/${incident.id}`;
  const act = (op: Op) => canAct(user, op, incident);
  const mayWriteItems = can(user, 'actionItem', incident) && incident.status !== 'CLOSED';
  const mayAddNote = can(user, 'note', incident) && incident.status !== 'CLOSED';

  return (
    <div>
      <div className="card">
        <h2><span data-testid="detail-number">{incident.number}</span> · <span data-testid="detail-title">{incident.title}</span></h2>
        <p data-testid="detail-description">{incident.description}</p>
        <div className="kv">
          <span>Status</span><b data-testid="detail-status">{incident.status}</b>
          <span>Severity</span><b data-testid="detail-severity">{incident.severity}</b>
          <span>Service</span><b data-testid="detail-service">{incident.serviceName}</b>
          <span>Assignee</span><b data-testid="detail-assignee">{name(incident.assigneeId) ?? 'Unassigned'}</b>
          <span>Reporter</span><b data-testid="detail-reporter">{name(incident.reporterId) ?? ''}</b>
          <span>Escalation level</span><b data-testid="detail-escalation-level">{incident.escalationLevel}</b>
          <span>Reopen count</span><b data-testid="detail-reopen-count">{incident.reopenCount}</b>
          <span>Ack SLA</span><b data-testid="detail-sla-ack">{incident.sla.ack.status}</b>
          <span>Ack deadline</span><b data-testid="detail-sla-ack-deadline" data-timestamp={incident.sla.ack.deadline}>{incident.sla.ack.deadline}</b>
          <span>Resolve SLA</span><b data-testid="detail-sla-resolve">{incident.sla.resolve.status}</b>
          <span>Resolve deadline</span><b data-testid="detail-sla-resolve-deadline" data-timestamp={incident.sla.resolve.deadline}>{incident.sla.resolve.deadline}</b>
        </div>
        {incident.resolutionSummary !== null && <p data-testid="detail-resolution-summary">{incident.resolutionSummary}</p>}
      </div>

      <div className="row">
        {act('acknowledge') && <button data-testid="action-acknowledge" onClick={() => run(() => post(`${base}/acknowledge`), 'Incident acknowledged.')}>Acknowledge</button>}
        {act('mitigate') && <button data-testid="action-mitigate" onClick={() => setDialog('mitigate')}>Mitigate</button>}
        {act('resolve') && <button data-testid="action-resolve" onClick={() => setDialog('resolve')}>Resolve</button>}
        {act('close') && <button data-testid="action-close" onClick={() => run(() => post(`${base}/close`), 'Incident closed.')}>Close</button>}
        {act('reopen') && <button data-testid="action-reopen" onClick={() => setDialog('reopen')}>Reopen</button>}
        {act('assign') && <button data-testid="action-assign" onClick={() => setDialog('assign')}>Assign</button>}
        {act('severity') && <button data-testid="action-severity" onClick={() => setDialog('severity')}>Change severity</button>}
        {act('edit') && <button data-testid="action-edit" onClick={() => setDialog('edit')}>Edit</button>}
      </div>

      {dialog && (
        <ActionDialog
          kind={dialog}
          incident={incident}
          users={users}
          onClose={() => setDialog(null)}
          onDone={async (message) => {
            setDialog(null);
            showToast(message);
            await load();
          }}
        />
      )}

      <div className="card">
        <h3>Notes</h3>
        {notes.map((n) => (
          <div key={n.id} data-testid="note-item" data-note-id={n.id}>
            <b data-testid="note-author">{name(n.authorId)}</b>: <span data-testid="note-body">{n.body}</span>
          </div>
        ))}
        {mayAddNote && <NoteForm incidentId={incident.id} onAdded={load} />}
      </div>

      <div className="card">
        <h3>Action items</h3>
        <div data-testid="ai-list">
          {items.map((a) => (
            <div key={a.id} data-testid="ai-item" data-item-id={a.id}>
              <span data-testid="ai-title">{a.title}</span> · <span data-testid="ai-owner">{name(a.ownerId)}</span> · due <span data-testid="ai-due">{a.dueDate}</span> ·{' '}
              <b data-testid="ai-status">{a.status}</b>
              {mayWriteItems && (
                <button
                  data-testid="ai-toggle"
                  onClick={() => run(() => patch(`${base}/action-items/${a.id}`, { status: a.status === 'OPEN' ? 'DONE' : 'OPEN' }), 'Action item updated.')}
                >
                  {a.status === 'OPEN' ? 'Mark done' : 'Reopen'}
                </button>
              )}
            </div>
          ))}
        </div>
        {mayWriteItems && <ActionItemForm incidentId={incident.id} owners={users.filter((u) => ['RESPONDER', 'COMMANDER', 'ADMIN'].includes(u.role))} onAdded={load} />}
      </div>

      {audit && (
        <div className="card" data-testid="audit-list">
          <h3>Audit trail</h3>
          {audit.map((e) => (
            <div key={e.id} data-testid="audit-item">
              <span data-testid="audit-action">{e.action}</span> by <span data-testid="audit-actor">{e.actorId === null ? 'System' : name(e.actorId)}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function NoteForm({ incidentId, onAdded }: { incidentId: number; onAdded: () => Promise<void> }) {
  const { showToast } = useSession();
  const [body, setBody] = useState('');
  const submit = async () => {
    try {
      await post(`/incidents/${incidentId}/notes`, { body });
      setBody('');
      showToast('Note added.');
      await onAdded();
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : 'Something went wrong.');
    }
  };
  return (
    <div className="row">
      <input data-testid="note-input" value={body} onChange={(e) => setBody(e.target.value)} placeholder="Add a note" />
      <button data-testid="note-submit" onClick={submit}>Add note</button>
    </div>
  );
}

function ActionItemForm({ incidentId, owners, onAdded }: { incidentId: number; owners: User[]; onAdded: () => Promise<void> }) {
  const [title, setTitle] = useState('');
  const [ownerId, setOwnerId] = useState('');
  const [due, setDue] = useState('');
  const [error, setError] = useState<string | null>(null);
  const submit = async () => {
    setError(null);
    try {
      await post(`/incidents/${incidentId}/action-items`, { title, ownerId: ownerId ? Number(ownerId) : undefined, dueDate: due });
      setTitle('');
      setDue('');
      await onAdded();
    } catch (e) {
      const err = e as ApiError;
      setError(err.errors?.length ? err.errors.map((f) => f.message).join(' ') : err.message ?? 'Something went wrong.');
    }
  };
  return (
    <div className="row">
      <input data-testid="ai-title-input" value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Action item title" />
      <select data-testid="ai-owner-select" value={ownerId} onChange={(e) => setOwnerId(e.target.value)}>
        <option value="">Owner</option>
        {owners.map((u) => <option key={u.id} value={String(u.id)}>{u.displayName}</option>)}
      </select>
      <input data-testid="ai-due-input" value={due} onChange={(e) => setDue(e.target.value)} placeholder="YYYY-MM-DD" />
      <button data-testid="ai-add" onClick={submit}>Add</button>
      {error && <span data-testid="ai-error" className="field-error">{error}</span>}
    </div>
  );
}

interface ActionDialogProps {
  kind: DialogKind;
  incident: Incident;
  users: User[];
  onClose: () => void;
  onDone: (message: string) => Promise<void>;
}

function ActionDialog({ kind, incident, users, onClose, onDone }: ActionDialogProps) {
  const [input, setInput] = useState(kind === 'edit' ? incident.title : '');
  const [secondary, setSecondary] = useState(kind === 'edit' ? incident.description : '');
  const [select, setSelect] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const base = `/incidents/${incident.id}`;
  const assignees = users.filter((u) => (u.role === 'RESPONDER' || u.role === 'COMMANDER') && u.teamId === incident.teamId);

  const confirm = async () => {
    setBusy(true);
    setError(null);
    try {
      switch (kind) {
        case 'mitigate': await post(`${base}/mitigate`, { message: input }); break;
        case 'resolve': await post(`${base}/resolve`, { resolutionSummary: input }); break;
        case 'reopen': await post(`${base}/reopen`, { reason: input }); break;
        case 'assign': await post(`${base}/assign`, { assigneeId: select ? Number(select) : undefined }); break;
        case 'severity': await post(`${base}/severity`, { severity: select || undefined, reason }); break;
        case 'edit': await patch(base, { title: input, description: secondary }); break;
      }
      await onDone(`${TITLES[kind]}: done.`);
    } catch (e) {
      if (e instanceof ApiError) setError(e.errors.length ? e.errors.map((f) => f.message).join(' ') : e.message);
      else setError('Something went wrong.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog title={TITLES[kind]} error={error} busy={busy} onConfirm={confirm} onCancel={onClose}>
      {(kind === 'mitigate' || kind === 'resolve' || kind === 'reopen') && (
        <textarea data-testid="dialog-input" value={input} onChange={(e) => setInput(e.target.value)} />
      )}
      {kind === 'edit' && (
        <>
          <input data-testid="dialog-input" value={input} onChange={(e) => setInput(e.target.value)} />
          <textarea data-testid="dialog-input-secondary" value={secondary} onChange={(e) => setSecondary(e.target.value)} />
        </>
      )}
      {kind === 'assign' && (
        <select data-testid="dialog-select" value={select} onChange={(e) => setSelect(e.target.value)}>
          <option value="">Select assignee</option>
          {assignees.map((u) => <option key={u.id} value={String(u.id)}>{u.displayName}</option>)}
        </select>
      )}
      {kind === 'severity' && (
        <>
          <select data-testid="dialog-select" value={select} onChange={(e) => setSelect(e.target.value)}>
            <option value="">Select severity</option>
            {SEVERITIES.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
          <textarea data-testid="dialog-reason" value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Reason" />
        </>
      )}
    </Dialog>
  );
}
