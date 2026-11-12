import { Link, NavLink, Outlet } from 'react-router-dom'
import { MODE } from '../data'
import '../styles/showcase.css'

/** Presentation layout for Architecture, API and Security (top nav, centred content). */
export function ShowcaseLayout() {
  return (
    <div className="showcase">
      <header className="sc-nav">
        <Link to="/" className="sc-nav__brand">SellBot.lk</Link>
        <nav aria-label="Project" className="sc-nav__links">
          <NavLink to="/" end className="sc-nav__link">Overview</NavLink>
          <NavLink to="/architecture" className="sc-nav__link">Architecture</NavLink>
          <NavLink to="/api" className="sc-nav__link">API</NavLink>
          <NavLink to="/security" className="sc-nav__link">Security &amp; tests</NavLink>
        </nav>
        <Link to={MODE === 'live' ? '/login' : '/app'} className="btn btn--green btn--md sc-nav__cta">Enter demo</Link>
      </header>
      <main className="sc-main" id="main"><Outlet /></main>
    </div>
  )
}
