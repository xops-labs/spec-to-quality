import { useState, type FormEvent } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { ApiError } from '../api';
import { useSession } from '../session';

export default function Login() {
  const { user, login } = useSession();
  const navigate = useNavigate();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [failed, setFailed] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  if (user) return <Navigate to="/incidents" replace />;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setFailed(false);
    setMessage(null);
    try {
      await login(username, password);
      navigate('/incidents');
    } catch (err) {
      if (err instanceof ApiError && (err.code === 'INVALID_CREDENTIALS' || err.code === 'VALIDATION_ERROR')) setFailed(true);
      else setMessage('Sign-in is unavailable right now.');
    }
  };

  return (
    <main>
      <form onSubmit={submit} className="card" style={{ maxWidth: 360, margin: '3rem auto' }}>
        <h2>IncidentDesk</h2>
        <label>
          Username
          <input data-testid="login-username" value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" />
        </label>
        <label>
          Password
          <input data-testid="login-password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" />
        </label>
        {failed && <p data-testid="login-error" className="field-error">Invalid username or password.</p>}
        {message && <p className="field-error">{message}</p>}
        <div className="row">
          <button data-testid="login-submit" type="submit">Sign in</button>
        </div>
      </form>
    </main>
  );
}
