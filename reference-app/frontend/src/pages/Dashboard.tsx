import { useEffect, useState } from 'react';
import { get } from '../api';
import { useSession } from '../session';
import { SEVERITIES, STATUSES, type Dashboard as Summary } from '../types';

const one = (n: number | null) => (n === null ? 'N/A' : n.toFixed(1));

export default function Dashboard() {
  const { reportError } = useSession();
  const [d, setD] = useState<Summary | null>(null);

  useEffect(() => {
    get<Summary>('/dashboard/summary').then(setD).catch(reportError);
  }, [reportError]);

  if (!d) return <p>Loading…</p>;
  return (
    <div>
      <h2>Dashboard</h2>
      <div className="tiles">
        <div className="card">Active <b data-testid="dash-active-count">{d.activeCount}</b></div>
        <div className="card">Total <b data-testid="dash-total-count">{d.totalIncidents}</b></div>
        <div className="card">Breached <b data-testid="dash-breached-count">{d.breachedActiveCount}</b></div>
        <div className="card">At risk <b data-testid="dash-at-risk-count">{d.atRiskActiveCount}</b></div>
        <div className="card">Escalated <b data-testid="dash-escalated-count">{d.escalatedActiveCount}</b></div>
        <div className="card">MTTA (min) <b data-testid="dash-mtta">{one(d.mttaMinutes)}</b></div>
        <div className="card">MTTR (min) <b data-testid="dash-mttr">{one(d.mttrMinutes)}</b></div>
        <div className="card">SLA compliance <b data-testid="dash-sla-compliance">{d.slaCompliancePercent === null ? 'N/A' : `${one(d.slaCompliancePercent)}%`}</b></div>
      </div>
      <div className="card">
        <h3>By status</h3>
        {STATUSES.map((s) => <div key={s}>{s}: <b data-testid={`dash-status-${s}`}>{d.byStatus[s]}</b></div>)}
      </div>
      <div className="card">
        <h3>Active by severity</h3>
        {SEVERITIES.map((s) => <div key={s}>{s}: <b data-testid={`dash-severity-${s}`}>{d.activeBySeverity[s]}</b></div>)}
      </div>
      <div className="card">
        <h3>Top services</h3>
        {d.topServices.map((s) => (
          <div key={s.serviceId} data-testid="dash-top-service">{s.serviceName}: {s.activeCount}</div>
        ))}
      </div>
    </div>
  );
}
