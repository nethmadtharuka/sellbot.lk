const FLOW = [
  { title: 'WhatsApp', desc: 'Customer message in Sinhala, Tamil or English' },
  { title: 'Webhook', desc: 'Receives and validates the message' },
  { title: 'Gemini', desc: 'Classifies language and intent', ai: true },
  { title: 'Rules in code', desc: 'Price, min price, stock, expiry' },
  { title: 'Database', desc: 'Orders, payments, stock' },
]
const PILLARS = [
  { label: 'Guard rail', text: 'AI never sets a price or changes stock.' },
  { label: 'Owner in control', text: 'Payments are approved by a person.' },
  { label: 'Admin', text: 'One private dashboard for the owner.' },
]

export default function Architecture() {
  return (
    <>
      <section className="sc-hero" data-anim="hero" aria-labelledby="arch-t">
        <p className="eyebrow">Architecture</p>
        <h1 id="arch-t" className="sc-title">How a message becomes<br />an order</h1>
        <p className="sc-sub">AI only labels the message. Business rules in code make every decision about price, stock and payment.</p>
      </section>
      <ol className="flow" aria-label="Message pipeline">
        {FLOW.map((f) => (
          <li key={f.title} className={`flow__node${f.ai ? ' flow__node--ai' : ''}`} data-anim="flow-node">
            <h2 className="flow__title">{f.title}</h2>
            <p className="flow__desc">{f.desc}</p>
          </li>
        ))}
      </ol>
      <div className="pillars">
        {PILLARS.map((p) => (
          <article key={p.label} className="pillar" data-anim="pillar">
            <p className="eyebrow">{p.label}</p>
            <p className="pillar__text">{p.text}</p>
          </article>
        ))}
      </div>
    </>
  )
}
