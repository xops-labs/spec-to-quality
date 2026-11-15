export type Role = 'VIEWER' | 'REPORTER' | 'RESPONDER' | 'COMMANDER' | 'ADMIN';
export type Severity = 'SEV1' | 'SEV2' | 'SEV3' | 'SEV4';
export type Status = 'OPEN' | 'ACKNOWLEDGED' | 'MITIGATED' | 'RESOLVED' | 'CLOSED';
export type TimerStatus = 'RUNNING' | 'AT_RISK' | 'BREACHED' | 'MET';

export const SEVERITIES: Severity[] = ['SEV1', 'SEV2', 'SEV3', 'SEV4'];
export const STATUSES: Status[] = ['OPEN', 'ACKNOWLEDGED', 'MITIGATED', 'RESOLVED', 'CLOSED'];

export interface User {
  id: number;
  username: string;
  displayName: string;
  role: Role;
  teamId: number | null;
}

export interface Service {
  id: number;
  key: string;
  name: string;
  ownerTeamId: number;
  tier: number;
  active: boolean;
}

export interface Timer {
  targetMinutes: number;
  deadline: string;
  status: TimerStatus;
}

export interface Incident {
  id: number;
  number: string;
  title: string;
  description: string;
  serviceId: number;
  serviceName: string;
  teamId: number;
  severity: Severity;
  status: Status;
  reporterId: number;
  assigneeId: number | null;
  createdAt: string;
  updatedAt: string;
  acknowledgedAt: string | null;
  mitigatedAt: string | null;
  resolvedAt: string | null;
  closedAt: string | null;
  resolutionSummary: string | null;
  escalationLevel: number;
  reopenCount: number;
  version: number;
  actionItemCount: number;
  openActionItemCount: number;
  sla: { ack: Timer; resolve: Timer; breached: boolean };
}

export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface Note {
  id: number;
  incidentId: number;
  authorId: number;
  body: string;
  createdAt: string;
}

export interface ActionItem {
  id: number;
  incidentId: number;
  title: string;
  ownerId: number;
  dueDate: string;
  status: 'OPEN' | 'DONE';
  createdAt: string;
  completedAt: string | null;
}

export interface AuditEvent {
  id: number;
  incidentId: number | null;
  actorId: number | null;
  action: string;
  createdAt: string;
  details: Record<string, unknown>;
}

export interface Dashboard {
  generatedAt: string;
  totalIncidents: number;
  activeCount: number;
  byStatus: Record<Status, number>;
  activeBySeverity: Record<Severity, number>;
  breachedActiveCount: number;
  atRiskActiveCount: number;
  escalatedActiveCount: number;
  mttaMinutes: number | null;
  mttrMinutes: number | null;
  slaCompliancePercent: number | null;
  topServices: { serviceId: number; serviceName: string; activeCount: number }[];
}

export interface FieldError {
  field: string;
  code: string;
  message: string;
}
