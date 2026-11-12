const NUMS = [
  { value: 80, label: 'automated tests' },
  { value: 63, label: 'integration' },
  { value: 17, label: 'unit' },
]
const PROTECTIONS = ['Owner sign in with attempt limits', 'Secrets live on the server only', 'Min price never shown to customers', 'Payments approved by a person']
const TESTED = ['Price and negotiation rules', 'Stock changes on order and cancel', 'Order and payment status flow', 'Language and intent handling']

export default function Security() {
  return (
    <>
      <section className="sc-hero" data-anim="hero" aria-labelledby="sec-t">
        <p className="eyebrow">Security &amp; testing</p>
        <h1 id="sec-t" className="sc-title">Built to be trusted</h1>
        <p className="sc-sub">What protects the data, and how it is verified.</p>
      </section>
      <div className="nums">
        {NUMS.map((n) => (
          <div key={n.label} className="num-card" data-anim="num">
            <p className="num-card__value">{n.value}</p>
            <p className="num-card__label">{n.label}</p>
          </div>
        ))}
      </div>
      <div className="lists">
        <section className="list-card" aria-labelledby="prot-t"><h2 id="prot-t" className="card__title">Protections</h2><ul>{PROTECTIONS.map((p) => <li key={p}>{p}</li>)}</ul></section>
        <section className="list-card" aria-labelledby="test-t"><h2 id="test-t" className="card__title">What is tested</h2><ul>{TESTED.map((p) => <li key={p}>{p}</li>)}</ul></section>
      </div>
    </>
  )
}
