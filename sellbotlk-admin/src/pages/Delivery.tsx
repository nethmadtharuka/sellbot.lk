import { Link } from 'react-router-dom'
import { DEMO_NOW } from '../data/demo'
import { data } from '../data'
import { useAsync } from '../hooks/useAsync'
import { FULFILMENT_STEPS } from '../lib/status'
import { Card, InlineError, Mono, PageHeader, Skeleton, StatusBadge } from '../ui/primitives'

const WINDOW_DAYS = 7 // board shows this week's active orders

export default function Delivery() {
  const [state, reload] = useAsync(() => data.orders(), [])
  const now = data.mode === 'demo' ? DEMO_NOW : new Date()
  const since = now.getTime() - WINDOW_DAYS * 86_400_000
  const orders = state.status === 'ready' ? state.data.data.filter((o) => new Date(o.createdAt).getTime() >= since) : []

  return (
    <>
      <PageHeader eyebrow="Fulfilment" title="Delivery" />
      {state.status === 'loading' && <Card><Skeleton /></Card>}
      {state.status === 'error' && <InlineError title="Could not load deliveries" body="Check your connection and try again." onRetry={reload} />}
      {state.status === 'ready' && (
        <>
          <div className="board">
            {FULFILMENT_STEPS.map((s) => {
              const col = orders.filter((o) => o.status === s)
              return (
                <Card key={s} className="col" aria-label={`${s}, ${col.length} orders`}>
                  <div className="col__head"><StatusBadge status={s} /><span>{col.length}</span></div>
                  <ul className="col__cards">
                    {col.map((o) => {
                      const n = o.items.reduce((t, i) => t + i.qty, 0)
                      return (
                        <li key={o.id}>
                          <Link to={`/app/orders/${o.id}`} className="dcard">
                            <Mono className="dcard__no">{o.number}</Mono>
                            <p className="dcard__name">{o.customerName}</p>
                            <p className="dcard__items">{n} {n === 1 ? 'item' : 'items'}</p>
                          </Link>
                        </li>
                      )
                    })}
                  </ul>
                </Card>
              )
            })}
          </div>
          <p className="note" style={{ marginTop: 18 }}>Status only. No couriers, zones or route planning in this version.</p>
        </>
      )}
    </>
  )
}
