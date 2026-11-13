import { useEffect, useState } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { MODE, data } from '../data'
import { Icon, WhatsAppGlyph } from '../ui/Icon'
import type { IconName } from '../ui/iconData'
import { ModeBanner } from '../ui/primitives'

type NavItem = { to: string; label: string; icon: IconName; badge?: 'payments' | 'products' }
const NAV: NavItem[] = [
  // Home is the Overview page (/app); it is active only on /app itself.
  { to: '/app', label: 'Home', icon: 'house' },
  { to: '/app/orders', label: 'Orders', icon: 'shopping-bag' },
  { to: '/app/payments', label: 'Payments', icon: 'credit-card', badge: 'payments' },
  { to: '/app/delivery', label: 'Delivery', icon: 'truck' },
  { to: '/app/products', label: 'Products', icon: 'package', badge: 'products' },
  { to: '/app/customers', label: 'Customers', icon: 'users' },
  { to: '/app/inbox', label: 'Inbox', icon: 'message-circle' },
  { to: '/app/playground', label: 'AI Playground', icon: 'sparkle' },
  { to: '/app/analytics', label: 'Analytics', icon: 'chart-column' },
  { to: '/app/documents', label: 'Documents', icon: 'file' },
  { to: '/app/integrations', label: 'Integrations', icon: 'plug' },
]

/** Journey and States are not in the nav; the design highlights AI Playground while on Journey. */
function activeNavFor(path: string): string {
  if (path.startsWith('/app/journey')) return '/app/playground'
  const hit = [...NAV].reverse().find((n) => n.to !== '/app' && path.startsWith(n.to))
  return hit?.to ?? '/app'
}
function titleFor(path: string): string {
  if (path.startsWith('/app/journey')) return 'Order journey'
  return NAV.find((n) => n.to === activeNavFor(path))?.label ?? 'Home'
}

function useNavCounts() {
  const [counts, setCounts] = useState<{ payments: number; products: number }>({ payments: 0, products: 0 })
  useEffect(() => {
    let live = true
    Promise.all([data.orders(), data.products()]).then(([o, p]) => {
      if (live) setCounts({
        payments: o.data.filter((x) => x.payment === 'Pending review').length,
        products: p.data.filter((x) => x.stock <= x.lowStockThreshold).length,
      })
    }, () => { /* counts are decorative; the pages surface errors */ })
    return () => { live = false }
  }, [])
  return counts
}

function NavList({ variant, active, counts, onNavigate }: {
  variant: 'full' | 'rail' | 'drawer'; active: string; counts: { payments: number; products: number }; onNavigate?: () => void
}) {
  return (
    <ul className={`nav nav--${variant}`}>
      {NAV.map((n) => {
        const count = n.badge ? counts[n.badge] : 0
        const isActive = n.to === active
        return (
          <li key={n.to}>
            <NavLink to={n.to} end={n.to === '/app'} onClick={onNavigate}
              className={`nav__item${isActive ? ' is-active' : ''}`} aria-current={isActive ? 'page' : undefined}
              aria-label={variant === 'rail' ? `${n.label}${count ? `, ${count} need action` : ''}` : undefined}
              data-tooltip={variant === 'rail' ? n.label : undefined}>
              <Icon name={n.icon} size={variant === 'drawer' ? 18 : 20} />
              {variant !== 'rail' && <span className="nav__label">{n.label}</span>}
              {variant === 'full' && count > 0 && <span className="nav__badge" aria-label={`${count} need action`}>{count}</span>}
            </NavLink>
          </li>
        )
      })}
    </ul>
  )
}

export function AdminLayout() {
  const { pathname } = useLocation()
  const counts = useNavCounts()
  const [drawer, setDrawer] = useState(false)
  const active = activeNavFor(pathname)
  // The "About this project" card is drawn only on the Overview design.
  const showAbout = MODE === 'demo' && pathname === '/app'

  useEffect(() => {
    if (!drawer) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setDrawer(false) }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [drawer])

  return (
    <div className="admin-bg">
    <div className="shell">
      <a className="skip-link" href="#main">Skip to content</a>
      <aside className="sidebar" aria-label="Admin navigation">
        <nav aria-label="Primary"><NavList variant="full" active={active} counts={counts} /></nav>
        <div className="sidebar__foot">
          {showAbout && (
            <div className="about-card">
              <p className="about-card__title">About this project</p>
              <p className="about-card__links">
                <Link to="/app/journey">Journey</Link> · <Link to="/architecture">Architecture</Link> · <Link to="/api">API</Link> ·{' '}
                <Link to="/security">Security</Link>
              </p>
            </div>
          )}
          <Link to="/" className="nav__item nav__item--site"><Icon name="arrow-left" size={20} /><span className="nav__label">Back to site</span></Link>
        </div>
      </aside>

      <aside className="rail" aria-label="Admin navigation">
        <Link to="/" className="rail__logo" aria-label="Back to site" data-tooltip="Back to site"><Icon name="message-circle" size={22} strokeWidth={2} /></Link>
        <nav aria-label="Primary"><NavList variant="rail" active={active} counts={counts} /></nav>
      </aside>

      <header className="topbar">
        <button type="button" className="icon-btn" aria-label="Open navigation" aria-expanded={drawer} onClick={() => setDrawer(true)}>
          <Icon name="menu" size={24} />
        </button>
        <p className="topbar__title">{titleFor(pathname)}</p>
        <Link to="/app/orders" className="icon-btn" aria-label="Search orders" data-tooltip="Search"><Icon name="search" size={22} /></Link>
      </header>

      {drawer && (
        <div className="nav-drawer" onMouseDown={(e) => { if (e.target === e.currentTarget) setDrawer(false) }}>
          <div className="nav-drawer__panel" role="dialog" aria-modal="true" aria-label="Navigation">
            <div className="nav-drawer__head">
              <Link to="/" className="brand-mini" onClick={() => setDrawer(false)} aria-label="SellBot.lk, back to site"><WhatsAppGlyph size={18} /> SellBot.lk</Link>
              <button type="button" className="icon-btn" aria-label="Close navigation" onClick={() => setDrawer(false)}><Icon name="x" size={22} /></button>
            </div>
            <nav aria-label="Primary"><NavList variant="drawer" active={active} counts={counts} onNavigate={() => setDrawer(false)} /></nav>
            <Link to="/" className="nav__item nav__item--site nav-drawer__site" onClick={() => setDrawer(false)}><Icon name="arrow-left" size={18} /><span className="nav__label">Back to site</span></Link>
          </div>
        </div>
      )}

      <main className="main" id="main">
        <div className="main__banner"><ModeBanner mode={MODE} /></div>
        <div className="main__banner-compact"><ModeBanner mode={MODE} compact /></div>
        <Outlet />
      </main>
    </div>
    </div>
  )
}
