import { describe, expect, it } from 'vitest';
import { can, canAct, statusAllows } from '../permissions';
import { makeIncident, USERS } from './helpers';

const by = (username: string) => USERS.find((u) => u.username === username)!;

describe('permissions', () => {
  it('viewers can do nothing', () => {
    const incident = makeIncident();
    for (const op of ['create', 'edit', 'note', 'acknowledge', 'mitigate', 'resolve', 'close', 'reopen', 'assign', 'severity', 'actionItem', 'readAudit'] as const) {
      expect(can(by('viewer1'), op, incident)).toBe(false);
    }
  });

  it('responders act on their own team only and need assignment to mitigate', () => {
    const resp = by('resp.platform1');
    expect(can(resp, 'acknowledge', makeIncident({ teamId: 1 }))).toBe(true);
    expect(can(resp, 'acknowledge', makeIncident({ teamId: 2 }))).toBe(false);
    expect(can(resp, 'mitigate', makeIncident({ assigneeId: null }))).toBe(false);
    expect(can(resp, 'mitigate', makeIncident({ assigneeId: 5 }))).toBe(true);
    expect(can(resp, 'close', makeIncident())).toBe(false);
  });

  it('reporters edit only their own open incident', () => {
    const rep = by('reporter1');
    expect(can(rep, 'edit', makeIncident({ reporterId: 9, status: 'OPEN' }))).toBe(true);
    expect(can(rep, 'edit', makeIncident({ reporterId: 9, status: 'ACKNOWLEDGED' }))).toBe(false);
    expect(can(rep, 'edit', makeIncident({ reporterId: 3 }))).toBe(false);
    expect(can(rep, 'note', makeIncident({ reporterId: 9 }))).toBe(true);
  });

  it('admins can do everything and commanders are team-scoped', () => {
    expect(can(by('admin'), 'close', makeIncident({ teamId: 3 }))).toBe(true);
    expect(can(by('cmd.platform'), 'close', makeIncident({ teamId: 3 }))).toBe(false);
    expect(can(by('cmd.platform'), 'close', makeIncident({ teamId: 1 }))).toBe(true);
  });

  it('buttons depend on status as well', () => {
    expect(statusAllows('acknowledge', 'OPEN')).toBe(true);
    expect(statusAllows('acknowledge', 'ACKNOWLEDGED')).toBe(false);
    expect(statusAllows('resolve', 'ACKNOWLEDGED')).toBe(false);
    expect(statusAllows('reopen', 'RESOLVED')).toBe(true);
    expect(statusAllows('edit', 'CLOSED')).toBe(false);
    expect(canAct(by('admin'), 'assign', makeIncident({ status: 'RESOLVED' }))).toBe(false);
  });
});
