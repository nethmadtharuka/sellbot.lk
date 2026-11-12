import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { data } from '../data'
import type { Order, Overview, Product } from '../data/types'
import { useAsync } from '../hooks/useAsync'
import { AreaSpark } from '../ui/AreaSpark'
import { lkr, maskPhone } from '../lib/format'
import { ButtonLink, Card, Chip, InlineError, LangChip, Mono, PageHeader, SearchField, Skeleton, StatusBadge } from '../ui/primitives'

function greeting(now: Date): string {
  const h = now.getHours()
  return h < 12 ? 'Good morning, owner' : h < 18 ? 'Good afternoon, owner' : 'Good evening, owner'
}

export default function Overview() {
  const [state, reload] = useAsync(async () => {
    const [ov, orders, products] = await Promise.all([data.overview(), data.orders(), data.products()])
    const low = products.data.filter((p) => p.stock <= p.lowStockThreshold)
    // Recent orders = newest WhatsApp orders (admin-created orders are not shown, as drawn)
    return { ov: ov.data, recent: orders.data.filter((o) => o.channel !== 'Admin').slice(0, 5), low }
  }, [])
  const [q, setQ] = useState('')
  const navigate = useNavigate()
  // The design is captured in the afternoon; demo mode keeps that greeting.
  const title = data.mode === 'demo' ? 'Good afternoon, owner' : greeting(new Date())

  return (
    <>
      <div className="ov-head-tablet"><PageHeader eyebrow="Home" title="Overview" /></div>
      <div className="ov-head-desktop"><PageHeader eyebrow="Overview" title={title} right={
        <div className="ov-head-right">
          <form role="search" onSubmit={(e) => { e.preventDefault(); navigate(`/app/orders?q=${encodeURIComponent(q)}`) }}>
            <SearchField label="Search orders and customers" placeholder="Search orders, customers" value={q} onChange={setQ} />
          </form>
          <span className="kd-avatar" aria-label="Signed in as owner">KD</span>
        </div>
      } /></div>
      {state.status === 'loading' && <Card><Skeleton /></Card>}
      {state.status === 'error' && <InlineError title="Could not load the overview" body="Check your connection and try again." onRetry={reload} />}
      {state.status === 'ready' && <OverviewBody ov={state.data.ov} recent={state.data.recent} low={state.data.low} />}
    </>
  )
}

function OverviewBody({ ov, recent, low }: { ov: Overview; recent: Order[]; low: Product[] }) {
  const labels = ov.revenue14.filter((p) => p.label)
  return (
    <>
      <div className="ov-desktop">
        <div className="kpis" data-anim="kpis">
          <Card as="div" className="kpi kpi--green">
            <div className="kpi__top"><span>Revenue · 30 days</span><Chip tone="tag">Paid only</Chip></div>
            <p className="kpi__value">{lkr(ov.revenue30)}</p>
            <p className="kpi__sub">From {ov.revenueOrders} orders</p>
          </Card>
          <Card as="div" className="kpi">
            <div className="kpi__top"><span>Orders</span><Chip tone="tag">30 days</Chip></div>
            <p className="kpi__value">{ov.orders30}</p>
            <p className="kpi__sub">{ov.ordersBreakdown}</p>
          </Card>
          <Card as="div" className="kpi">
            <div className="kpi__top"><span>Payments to review</span><Chip tone="tag">Needs you</Chip></div>
            <p className="kpi__value">{ov.paymentsToReview}</p>
            <p className="kpi__sub">{ov.oldestWaiting}</p>
          </Card>
          <Card as="div" className="kpi">
            <div className="kpi__top"><span>Low stock</span><Chip tone="tag">{ov.lowStock} products</Chip></div>
            <p className="kpi__value">{ov.lowStock}</p>
            <p className="kpi__sub">At or below threshold</p>
          </Card>
        </div>

        <div className="ov-row">
          <Card className="ov-card" aria-labelledby="na-title">
            <div className="row-between"><h2 id="na-title" className="card__title card__title--display">Needs action</h2><span className="muted note">{ov.queues.length} queues</span></div>
            {ov.queues.map((q) => (
              <div className="queue" key={q.title}>
                <span className={`queue__num queue__num--${q.tone}`}>{q.count}</span>
                <div className="queue__text"><p className="queue__title">{q.title}</p><p className="queue__detail">{q.detail}</p></div>
                <ButtonLink to={q.to} variant="white" size="sm">{q.action}</ButtonLink>
              </div>
            ))}
          </Card>
          <Card className="ov-card" aria-labelledby="rev-title">
            <div className="row-between"><h2 id="rev-title" className="card__title card__title--display">Revenue · last 14 days</h2><span className="note muted">LKR</span></div>
            <div className="rev-chart"><AreaSpark values={ov.revenue14.map((p) => p.value)} label={`Revenue trend over the last 14 days, from ${ov.revenue14[0]?.value} to ${ov.revenue14.at(-1)?.value}`} /></div>
            <div className="chart-axis">{labels.map((l) => <span key={l.label}>{l.label}</span>)}</div>
          </Card>
        </div>

        <Card className="recent" aria-labelledby="ro-title">
          <div className="row-between"><h2 id="ro-title" className="card__title card__title--display">Recent orders</h2><Link to="/app/orders" className="btn btn--ghost">View all</Link></div>
          <div className="tbl-scroll" style={{ marginTop: 14 }}>
            <table className="tbl">
              <thead><tr><th>Order</th><th>Customer</th><th>Total</th><th>Order status</th><th>Payment</th><th>Lang</th></tr></thead>
              <tbody>
                {recent.map((o) => (
                  <tr key={o.id}>
                    <td><Link className="cell-link" to={`/app/orders/${o.id}`}><Mono>{o.number}</Mono></Link></td>
                    <td><span className="strong">{o.customerName}</span><span className="sub">{maskPhone(o.customerPhone)}</span></td>
                    <td className="strong">{lkr(o.total)}</td>
                    <td><StatusBadge status={o.status} /></td>
                    <td><StatusBadge status={o.payment} /></td>
                    <td>{o.lang ? <LangChip lang={o.lang} /> : <span className="dim">—</span>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      </div>

      {/* Tablet sheet (768-1199): its own arrangement, as drawn in TabletOverview */}
      <div className="ov-tablet">
        <div className="kpis--tablet">
          {[
            ['Revenue · 14 d', lkr(ov.revenue30)], ['Orders', String(ov.orders30)], ['Slips to review', String(ov.paymentsToReview)], ['Low stock', String(ov.lowStock)],
          ].map(([label, value]) => (
            <Card as="div" key={label} className="kpi">
              <p className="eyebrow">{label}</p><p className="kpi__value">{value}</p>
            </Card>
          ))}
        </div>
        <Card className="ov-card tablet-list" aria-labelledby="na-t">
          <h2 id="na-t" className="card__title" style={{ marginBottom: 12 }}>Needs action</h2>
          <div className="row-between"><span>{ov.paymentsToReview} payment slips to review</span><Link to="/app/payments" className="btn btn--ghost">Open</Link></div>
          {low.map((p) => (
            <div className="row-between" key={p.id}><span>{p.shortName ?? p.name} {p.stock === 0 ? 'out of stock' : `low stock (${p.stock})`}</span><Link to="/app/products" className="btn btn--ghost">Open</Link></div>
          ))}
        </Card>
        <Card className="recent" aria-labelledby="ro-t">
          <h2 id="ro-t" className="card__title">Recent orders</h2>
          <table className="tbl" style={{ marginTop: 10 }}>
            <thead><tr><th>Order</th><th>Customer</th><th>Status</th></tr></thead>
            <tbody>{recent.slice(0, 3).map((o) => (
              <tr key={o.id}><td><Link className="cell-link" to={`/app/orders/${o.id}`}><Mono>{o.number}</Mono></Link></td><td className="strong" style={{ fontWeight: 400 }}>{o.customerName}</td><td><StatusBadge status={o.status} /></td></tr>
            ))}</tbody>
          </table>
        </Card>
      </div>
    </>
  )
}
