import { useShowcaseMotion } from '../motion/showcaseMotion'
/** Simplified, illustrative surface exactly as the design presents it (not the full reference). */
const ROUTES: { method: 'GET' | 'POST' | 'PATCH'; path: string; desc: string }[] = [
  { method: 'POST', path: '/webhook', desc: 'Receives WhatsApp messages' },
  { method: 'GET', path: '/api/orders', desc: 'List orders' },
  { method: 'PATCH', path: '/api/orders/:id', desc: 'Update order status' },
  { method: 'POST', path: '/api/payments/:id/approve', desc: 'Approve a slip' },
  { method: 'GET', path: '/api/products', desc: 'List products' },
  { method: 'PATCH', path: '/api/products/:id', desc: 'Edit price, min price, stock' },
]

export default function Api() {
  useShowcaseMotion()
  return (
    <>
      <section className="sc-hero" data-anim="hero" aria-labelledby="api-t">
        <p className="eyebrow">API</p>
        <h1 id="api-t" className="sc-title">The surface, at a glance</h1>
        <p className="sc-sub">Simplified overview for readers. Names are illustrative of the system, not a full reference.</p>
      </section>
      <ul className="api-card" aria-label="Endpoints">
        {ROUTES.map((r) => (
          <li key={r.method + r.path} className="api-row">
            <span className={`method method--${r.method.toLowerCase()}`}>{r.method}</span>
            <code className="api-path">{r.path}</code>
            <span className="api-desc">{r.desc}</span>
          </li>
        ))}
      </ul>
    </>
  )
}
