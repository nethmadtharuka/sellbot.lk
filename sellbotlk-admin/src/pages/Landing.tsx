import { useLayoutEffect, useRef } from 'react'
import { Link } from 'react-router-dom'
import { MODE } from '../data'
import { motionMode, useSmoothScroll } from '../motion/core'
import { initLanding } from '../motion/landingMotion'
import { Count } from '../ui/Count'
import { Icon, WhatsAppGlyph } from '../ui/Icon'
import '../styles/landing.css'
import '../styles/motion.css'

const GITHUB_URL = 'https://github.com/nethmadtharuka/sellbot.lk'
const ENTER = MODE === 'live' ? '/login' : '/app'

const STACK = ['ASP.NET Core 10', 'EF Core + MySQL 8', 'Google Gemini', 'WhatsApp Cloud API', 'React 19', 'TypeScript', 'Vite', 'Recharts', 'xUnit', 'GitHub Actions']

export default function Landing() {
  const root = useRef<HTMLDivElement>(null)
  useSmoothScroll(true)
  // Layout effect: initial animation states are set before the first paint (no flash).
  useLayoutEffect(() => (root.current ? initLanding(root.current, motionMode()) : undefined), [])
  return (
    <div className="landing" ref={root}>
      {/* Motion-only decoration (hidden unless motion runs): glass sheen */}
      <div className="sheen" aria-hidden="true" />
      <div className="hero-frame">
      <div className="hero-frame__inner">
      <section className="hero" data-anim="hero" aria-labelledby="hero-title">
        <div className="hero__arc" aria-hidden="true" />
        <header className="hero-nav">
          <Link to="/" className="brand" aria-label="SellBot.lk home">
            <span className="brand__tile"><WhatsAppGlyph size={22} /></span>
            <span className="brand__name">SellBot.lk</span>
          </Link>
          <nav className="hero-nav__pill" aria-label="Site">
            <Link to="/" className="hero-nav__link is-active" aria-current="page">Home</Link>
            <Link to="/app/journey" className="hero-nav__link">Journey</Link>
            <Link to="/architecture" className="hero-nav__link">Architecture</Link>
            <Link to="/api" className="hero-nav__link">API</Link>
            <Link to="/security" className="hero-nav__link">Security</Link>
          </nav>
          <Link to={ENTER} className="btn btn--green btn--md hero-nav__cta">Enter demo</Link>
        </header>

        <div className="hero__copy">
          <h1 id="hero-title" className="hero__title" data-anim="hero-title">Orders, handled<br />on WhatsApp.</h1>
          <p className="hero__sub" data-anim="hero-sub">
            Customers chat in Sinhala, Tamil or English. Gemini understands them. The code decides prices, stock and payments.
            The owner stays in control.
          </p>
          <div className="hero__actions" data-anim="hero-actions">
            <Link to={ENTER} className="btn btn--white btn--lg hero__btn">Enter demo <Icon name="arrow-up-right" size={16} strokeWidth={2} /></Link>
            <a href={GITHUB_URL} className="btn btn--lg hero__btn hero__btn--glass" target="_blank" rel="noreferrer">View on GitHub</a>
          </div>
        </div>

        <aside className="float-chip float-chip--guard" data-anim="chip-1" aria-label="Guard rail">
          <p className="float-chip__label">GUARD RAIL</p>
          <p className="float-chip__text">AI classifies. Code decides price, stock and payment.</p>
        </aside>
        <aside className="float-chip float-chip--lang" data-anim="chip-2" aria-label="Language and intent">
          <p className="float-chip__label">LANGUAGE + INTENT</p>
          <p className="float-chip__row">
            <span className="float-chip__tag" lang="si">සිංහල</span>
            <span className="float-chip__tag float-chip__tag--violet">Order</span>
            <span className="mono float-chip__conf">0.96</span>
          </p>
        </aside>
        <aside className="float-chip float-chip--tests" data-anim="chip-3" aria-label="Tests">
          <span className="ring" aria-hidden="true"><svg viewBox="0 0 52 52" width="52" height="52"><circle cx="26" cy="26" r="22" /><circle cx="26" cy="26" r="22" className="ring__arc" /></svg><b><Count value={80} /></b></span>
          <span><span className="float-chip__strong">Tests</span><span className="float-chip__small"><Count value={63} /> integration · <Count value={17} /> unit</span></span>
        </aside>

        <p className="wordmark" aria-hidden="true" data-anim="wordmark">SellBot</p>
      </section>
      </div>
      </div>

      <section className="how" data-anim="how" aria-labelledby="how-title">
        <div className="how__head">
          <h2 id="how-title" className="how__title">A platform that talks to customers <em>in their own language</em></h2>
          <p className="how__lede">Three steps from a WhatsApp message to a paid, delivered order. Every step runs in the real code.</p>
        </div>
        <div className="how__stage">
        <div className="how-ring" aria-hidden="true" />
        <ol className="how__cards">
          <li className="how-card" data-anim="how-card">
            <span className="how-card__num mono">01</span>
            <span className="how-card__icon"><WhatsAppGlyph size={22} /></span>
            <h3 className="how-card__title how-card__title--low">Customer chats</h3>
            <p className="how-card__body">Text, photos and payment slips in Sinhala, Tamil or English arrive through the WhatsApp Cloud API.</p>
          </li>
          <li className="how-card how-card--dark" data-anim="how-card">
            <span className="how-card__num mono">02</span>
            <h3 className="how-card__title">AI understands</h3>
            <p className="how-card__body">Gemini returns intent, language and confidence. Deterministic code enforces price floors, stock and ownership.</p>
          </li>
          <li className="how-card" data-anim="how-card">
            <span className="how-card__num mono">03</span>
            <h3 className="how-card__title">Owner manages</h3>
            <p className="how-card__body">Orders, payment-slip review, delivery and stock in one admin. AI can never mark an order paid.</p>
          </li>
        </ol>
        </div>
      </section>

      <section className="pick" data-anim="pick" aria-labelledby="pick-title">
        <div className="pick__head">
          <p className="eyebrow pick__eyebrow">Explore the project</p>
          <div className="pick__row">
            <h2 id="pick-title" className="pick__title">Pick where to start</h2>
            {/* Motion-only ticker of the tile's existing languages, plus the WhatsApp tile dropping in */}
            <span className="ticker" role="img" aria-label="Sinhala, Tamil, English">
              {['Sinhala', 'Tamil', 'English'].map((w) => <span key={w} className="ticker__word" aria-hidden="true">{w}</span>)}
            </span>
            <span className="brand__tile pick__icon" aria-hidden="true"><WhatsAppGlyph size={22} /></span>
          </div>
        </div>
        <div className="pick__tiles">
          <article className="tile tile--1" data-anim="tile">
            <span className="tile__num mono">01</span>
            <h3 className="tile__title">Admin dashboard</h3>
            <p className="tile__body">Orders, payment review, delivery board, products and analytics, on fictional data.</p>
            <div className="tile__foot"><span className="tile__meta"><Count value={14} /> pages</span><Link to={ENTER} className="btn btn--white btn--md tile__open" aria-label="Open admin dashboard">Open</Link></div>
          </article>
          <article className="tile tile--2" data-anim="tile">
            <span className="tile__num mono">02</span>
            <h3 className="tile__title">WhatsApp + AI</h3>
            <p className="tile__body">A simulated inbox, an AI playground and a step-by-step customer journey player.</p>
            <div className="tile__foot"><span className="tile__meta">Sinhala · Tamil · English</span><Link to="/app/inbox" className="btn btn--white btn--md tile__open" aria-label="Open WhatsApp and AI">Open</Link></div>
          </article>
          <article className="tile tile--3" data-anim="tile">
            <span className="tile__num mono">03</span>
            <h3 className="tile__title">Engineering</h3>
            <p className="tile__body">Architecture, API reference, webhook defences and the tests that prove them.</p>
            <div className="tile__foot"><span className="tile__meta"><Count value={80} /> tests</span><Link to="/architecture" className="btn btn--white btn--md tile__open" aria-label="Open engineering">Open</Link></div>
          </article>
        </div>
      </section>

      <section className="built" data-anim="built" aria-labelledby="built-title">
        <h2 id="built-title" className="eyebrow built__label">Built with</h2>
        <ul className="built__chips">{STACK.map((s) => <li key={s} className="built__chip">{s}</li>)}</ul>
        <p className="built__note"><Icon name="info" size={18} />Fictional data. Real system architecture. Nothing on this site connects to production.</p>
      </section>
    </div>
  )
}
