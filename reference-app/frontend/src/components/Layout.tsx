import { Link, Navigate, Outlet } from 'react-router-dom';
import { useSession } from '../session';

/** Frame for authenticated pages: navigation, current user, banners. Redirects to /login when there is no session. */
export default function Layout() {
  const { user, loading, logout, error, toast } = useSession();
  if (loading) return null;
  if (!user) return <Navigate to="/login" replace />;
  return (
    <div className="app">
      <header className="topbar">
        <nav>
          <Link data-testid="nav-incidents" to="/incidents">Incidents</Link>
          <Link data-testid="nav-dashboard" to="/dashboard">Dashboard</Link>
          {user.role !== 'VIEWER' && (
            <Link data-testid="nav-new-incident" to="/incidents/new">New incident</Link>
          )}
        </nav>
        <div className="who">
          <span data-testid="current-user">{user.displayName}</span>
          <span data-testid="current-role">{user.role}</span>
          <button data-testid="logout-button" onClick={logout}>Log out</button>
        </div>
      </header>
      {error && <div data-testid="error-banner" className="banner error">{error}</div>}
      {toast && <div data-testid="toast" className="toast" role="status">{toast}</div>}
      <main>
        <Outlet />
      </main>
    </div>
  );
}
