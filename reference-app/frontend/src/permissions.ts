import type { Incident, Role, User } from './types';

/** UI mirror of the permission matrix. A convenience only: the API is authoritative. */
export type Op =
  | 'create' | 'edit' | 'note' | 'acknowledge' | 'mitigate' | 'resolve' | 'close' | 'reopen'
  | 'assign' | 'severity' | 'actionItem' | 'readAudit';

const team = (u: User, i: Incident) => u.teamId !== null && u.teamId === i.teamId;

export function can(user: User, op: Op, incident?: Incident): boolean {
  const role: Role = user.role;
  if (op === 'create') return role !== 'VIEWER';
  if (!incident) return false;
  if (role === 'ADMIN') return true;
  switch (op) {
    case 'edit':
      return (role === 'REPORTER' && incident.reporterId === user.id && incident.status === 'OPEN') || (role === 'COMMANDER' && team(user, incident));
    case 'note':
      return (role === 'REPORTER' && incident.reporterId === user.id) || ((role === 'RESPONDER' || role === 'COMMANDER') && team(user, incident));
    case 'acknowledge':
    case 'actionItem':
    case 'readAudit':
      return (role === 'RESPONDER' || role === 'COMMANDER') && team(user, incident);
    case 'mitigate':
    case 'resolve':
      return (role === 'COMMANDER' && team(user, incident)) || (role === 'RESPONDER' && team(user, incident) && incident.assigneeId === user.id);
    case 'close':
    case 'reopen':
    case 'assign':
    case 'severity':
      return role === 'COMMANDER' && team(user, incident);
    default:
      return false;
  }
}

/** Which lifecycle/assignment buttons the status allows (spec 12.6). */
export function statusAllows(op: Op, status: Incident['status']): boolean {
  switch (op) {
    case 'acknowledge': return status === 'OPEN';
    case 'mitigate': return status === 'ACKNOWLEDGED';
    case 'resolve': return status === 'MITIGATED';
    case 'close':
    case 'reopen': return status === 'RESOLVED';
    case 'assign':
    case 'severity': return status === 'OPEN' || status === 'ACKNOWLEDGED' || status === 'MITIGATED';
    case 'edit':
    case 'note':
    case 'actionItem': return status !== 'CLOSED';
    default: return true;
  }
}

export const canAct = (user: User, op: Op, incident: Incident) => can(user, op, incident) && statusAllows(op, incident.status);
