import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { clearToken } from '../api/http'

const navItems: Array<{ to: string; label: string }> = [
  { to: '/', label: 'Dashboard' },
  { to: '/orders', label: 'Orders' },
  { to: '/delivery-zones', label: 'Delivery Zones' },
]

export function AppLayout() {
  const navigate = useNavigate()

  const handleLogout = () => {
    clearToken()
    navigate('/login', { replace: true })
  }

  return (
    <div className="appShell">
      <header className="topbar">
        <div className="brand">
          <div className="brandMark">SB</div>
          <div className="brandText">
            <div className="brandName">SellBotLK</div>
            <div className="brandSub">Admin Dashboard</div>
          </div>
        </div>
        <nav className="nav">
          {navItems.map((n) => (
            <NavLink
              key={n.to}
              to={n.to}
              end={n.to === '/'}
              className={({ isActive }) => `navLink ${isActive ? 'active' : ''}`}
            >
              {n.label}
            </NavLink>
          ))}
          <button onClick={handleLogout} className="navLink" style={{ background: 'none', border: 'none', cursor: 'pointer' }}>
            Logout
          </button>
        </nav>
      </header>

      <main className="content">
        <Outlet />
      </main>
    </div>
  )
}

