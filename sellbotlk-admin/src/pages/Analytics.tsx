import { data } from '../data'
import { useAsync } from '../hooks/useAsync'
import type { OrderStatus } from '../lib/status'
import { Card, Chip, InlineError, Meter, PageHeader, Skeleton } from '../ui/primitives'

const STATUS_TONE: Record<OrderStatus, 'green' | 'blue' | 'violet' | 'teal' | 'red'> = {
  Confirmed: 'green', Processing: 'blue', Dispatched: 'violet', Delivered: 'teal', Cancelled: 'red',
}

export default function Analytics() {
  const [state, reload] = useAsync(() => data.analytics(), [])
  return (
    <>
      <PageHeader eyebrow="Insights" title="Analytics" />
      {state.status === 'loading' && <Card><Skeleton /></Card>}
      {state.status === 'error' && <InlineError title="Could not load analytics" body="Check your connection and try again." onRetry={reload} />}
      {state.status === 'ready' && (() => {
        const a = state.data.data
        const max = Math.max(1, ...a.ordersPerDay)
        const topMax = Math.max(1, ...a.topProducts.map((t) => t.sold))
        return (
          <>
            <div className="an-grid-1">
              <Card className="card-pad" aria-labelledby="opd-t" data-anim="chart">
                <h2 id="opd-t" className="card__title">Orders per day · last 14 days</h2>
                <p className="note">{data.mode === 'demo' ? 'Computed from demo orders' : 'Computed from orders'}</p>
                <div className="bars" role="img" aria-label={`Orders per day for the last 14 days: ${a.ordersPerDay.map((v) => Math.round(v)).join(', ')}. The last bar is today.`}>
                  {a.ordersPerDay.map((v, i) => (
                    <span key={i} className={`bars__bar${i === a.ordersPerDay.length - 1 ? ' bars__bar--today' : ''}`} style={{ height: `${(v / max) * 100}%` }} />
                  ))}
                </div>
              </Card>
              <Card className="card-pad" aria-labelledby="obs-t">
                <h2 id="obs-t" className="card__title">Orders by status</h2>
                {a.byStatus.map((s) => (
                  <div className="mrow" key={s.status}>
                    <p className="mrow__label"><span>{s.status}</span><span>{s.count}</span></p>
                    <Meter value={s.count} max={a.totalOrders} tone={STATUS_TONE[s.status]} label={`${s.status}: ${s.count} of ${a.totalOrders}`} />
                  </div>
                ))}
              </Card>
            </div>
            <div className="an-grid-2">
              <Card className="card-pad" aria-labelledby="tp-t">
                <h2 id="tp-t" className="card__title">Top products</h2>
                {a.topProducts.map((t) => (
                  <div className="mrow" key={t.name}>
                    <p className="mrow__label"><span>{t.name}</span><span>{t.sold} sold</span></p>
                    <Meter value={t.sold} max={topMax} label={`${t.name}: ${t.sold} sold`} />
                  </div>
                ))}
              </Card>
              <Card className="card-pad" aria-labelledby="cl-t">
                <h2 id="cl-t" className="card__title">Customer language{data.mode === 'live' && a.languagesSimulated && <span className="sim-tag"><Chip tone="violet">Simulated</Chip></span>}</h2>
                {a.languages.map((l) => (
                  <div className="mrow" key={l.label}>
                    <p className="mrow__label"><span>{l.label}</span><span>{l.pct}%</span></p>
                    <Meter value={l.pct} max={100} tone="violet" label={`${l.label}: ${l.pct}%`} />
                  </div>
                ))}
                <p className="note" style={{ marginTop: 16 }}>No uptime or integration metrics are tracked.</p>
              </Card>
            </div>
          </>
        )
      })()}
    </>
  )
}
