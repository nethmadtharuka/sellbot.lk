import { NavLink, Outlet } from 'react-router-dom'

const navItems: Array<{ to: string; label: string }> = [
  { to: '/orders', label: 'Orders' },
  { to: '/delivery-zones', label: 'Delivery Zones' },
]

export function AppLayout() {
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
              className={({ isActive }) => `navLink ${isActive ? 'active' : ''}`}
            >
              {n.label}
            </NavLink>
          ))}
        </nav>
      </header>

      <main className="content">
        <Outlet />
      </main>
    </div>
  )
}

