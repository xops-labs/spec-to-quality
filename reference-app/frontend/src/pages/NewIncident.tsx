import { useEffect, useState, type FormEvent } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { ApiError, get, post } from '../api';
import { useSession } from '../session';
import { SEVERITIES, type Incident, type Service } from '../types';

type Errors = Partial<Record<'title' | 'description' | 'serviceId' | 'severity', string>>;

export function validate(title: string, description: string, serviceId: string, severity: string): Errors {
  const errors: Errors = {};
  const t = title.trim();
  if (!t) errors.title = 'Title is required.';
  else if (t.length < 5) errors.title = 'Title must be at least 5 characters.';
  else if (t.length > 120) errors.title = 'Title must be at most 120 characters.';
  if (description.trim().length > 4000) errors.description = 'Description must be at most 4000 characters.';
  if (!serviceId) errors.serviceId = 'Service is required.';
  if (!severity) errors.severity = 'Severity is required.';
  return errors;
}

export default function NewIncident() {
  const { user, reportError } = useSession();
  const navigate = useNavigate();
  const [services, setServices] = useState<Service[]>([]);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [serviceId, setServiceId] = useState('');
  const [severity, setSeverity] = useState('');
  const [errors, setErrors] = useState<Errors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    get<Service[]>('/services?active=true').then(setServices).catch(reportError);
  }, [reportError]);

  if (user?.role === 'VIEWER') return <Navigate to="/incidents" replace />;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setFormError(null);
    const clientErrors = validate(title, description, serviceId, severity);
    setErrors(clientErrors);
    if (Object.keys(clientErrors).length > 0) return;

    setBusy(true);
    try {
      const created = await post<Incident>('/incidents', { title, description, serviceId: Number(serviceId), severity });
      navigate(`/incidents/${created.id}`);
    } catch (err) {
      if (err instanceof ApiError && err.code === 'VALIDATION_ERROR') {
        const mapped: Errors = {};
        for (const fe of err.errors) {
          if (fe.field in { title: 1, description: 1, serviceId: 1, severity: 1 }) mapped[fe.field as keyof Errors] = fe.message;
        }
        setErrors(mapped);
        if (Object.keys(mapped).length === 0) setFormError(err.message);
      } else if (err instanceof ApiError && (err.code === 'BUSINESS_RULE_VIOLATION' || err.code === 'DUPLICATE_INCIDENT')) {
        setFormError(err.message);
      } else {
        reportError(err);
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={submit} className="card" noValidate>
      <h2>New incident</h2>
      <label>
        Title
        <input data-testid="new-title" value={title} onChange={(e) => setTitle(e.target.value)} />
      </label>
      {errors.title && <p data-testid="error-title" className="field-error">{errors.title}</p>}
      <label>
        Description
        <textarea data-testid="new-description" value={description} onChange={(e) => setDescription(e.target.value)} />
      </label>
      {errors.description && <p data-testid="error-description" className="field-error">{errors.description}</p>}
      <label>
        Service
        <select data-testid="new-service" value={serviceId} onChange={(e) => setServiceId(e.target.value)}>
          <option value="">Select a service</option>
          {services.filter((s) => s.active).map((s) => <option key={s.id} value={String(s.id)}>{s.name}</option>)}
        </select>
      </label>
      {errors.serviceId && <p data-testid="error-serviceId" className="field-error">{errors.serviceId}</p>}
      <label>
        Severity
        <select data-testid="new-severity" value={severity} onChange={(e) => setSeverity(e.target.value)}>
          <option value="">Select a severity</option>
          {SEVERITIES.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
      </label>
      {errors.severity && <p data-testid="error-severity" className="field-error">{errors.severity}</p>}
      {formError && <p data-testid="form-error" className="field-error">{formError}</p>}
      <div className="row">
        <button data-testid="new-submit" type="submit" disabled={busy}>Create</button>
      </div>
    </form>
  );
}
