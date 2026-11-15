import type { ReactNode } from 'react';

interface Props {
  title: string;
  error: string | null;
  busy: boolean;
  onConfirm: () => void;
  onCancel: () => void;
  children: ReactNode;
}

/** Modal used by the incident actions that need input. It stays open and shows the server message on failure. */
export default function Dialog({ title, error, busy, onConfirm, onCancel, children }: Props) {
  return (
    <div className="backdrop">
      <div data-testid="dialog" role="dialog" aria-modal="true" aria-label={title} className="dialog">
        <h3>{title}</h3>
        {children}
        {error && <p data-testid="dialog-error" className="field-error">{error}</p>}
        <div className="row">
          <button data-testid="dialog-confirm" disabled={busy} onClick={onConfirm}>Confirm</button>
          <button data-testid="dialog-cancel" onClick={onCancel}>Cancel</button>
        </div>
      </div>
    </div>
  );
}
