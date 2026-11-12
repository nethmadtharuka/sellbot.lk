import { useMemo, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { DEMO_NOW } from '../data/demo'
import { data } from '../data'
import { useAsync } from '../hooks/useAsync'
import { lkr, maskPhone, shortDate } from '../lib/format'
import { expiryOf } from '../lib/orders'
import { ORDER_STATUSES, type OrderStatus, type PaymentStatus } from '../lib/status'
import { Button, Card, Chip, EmptyState, FilterChip, InlineError, Mono, PageHeader, PillTabs, SearchField, Skeleton, StatusBadge } from '../ui/primitives'

const PAGE = 7
type Tab = 'All' | OrderStatus

export default function Orders() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const [state, reload] = useAsync(() => data.orders(), [])
  const [tab, setTab] = useState<Tab>('All')
  const [pay, setPay] = useState<PaymentStatus | null>((params.get('payment') as PaymentStatus | null) ?? null)
  const [q, setQ] = useState(params.get('q') ?? '')
  const [page, setPage] = useState(0)
  const now = data.mode === 'demo' ? DEMO_NOW : new Date()

  const all = useMemo(() => (state.status === 'ready' ? state.data.data : []), [state])
  const counts = useMemo(() => Object.fromEntries(ORDER_STATUSES.map((s) => [s, all.filter((o) => o.status === s).length])), [all])
  const rows = useMemo(() => {
    const needle = q.trim().toLowerCase()
    return all.filter((o) => (tab === 'All' || o.status === tab) && (!pay || o.payment === pay) &&
      (!needle || o.number.toLowerCase().includes(needle) || o.customerName.toLowerCase().includes(needle) || o.customerPhone.includes(needle)))
  }, [all, tab, pay, q])
  const pages = Math.max(1, Math.ceil(rows.length / PAGE))
  const view = rows.slice(page * PAGE, page * PAGE + PAGE)
  const reset = () => setPage(0)

  return (
    <>
      <div className="orders-head">
        <PageHeader eyebrow="Commerce" title="Orders" right={<SearchField label="Search orders" placeholder="Order number or customer" value={q} onChange={(v) => { setQ(v); reset() }} />} />
      </div>
      <div className="orders-tabs">
        <PillTabs<Tab> label="Order status" value={tab} onChange={(t) => { setTab(t); reset() }}
          items={[{ value: 'All', label: 'All', count: all.length }, ...ORDER_STATUSES.map((s) => ({ value: s as Tab, label: s, count: counts[s] }))]} />
      </div>
      <div className="pay-filter" role="group" aria-label="Payment filter">
        <span className="pay-filter__label">Payment</span>
        {(['Unpaid', 'Pending review', 'Paid'] as const).map((p) => (
          <FilterChip key={p} tone={p === 'Unpaid' ? 'unpaid' : p === 'Paid' ? 'paid' : 'review'} pressed={pay === p} onClick={() => { setPay(pay === p ? null : p); reset() }}>{p}</FilterChip>
        ))}
      </div>

      {state.status === 'loading' && <Card><Skeleton /></Card>}
      {state.status === 'error' && <InlineError title="Could not load orders" body="Check your connection and try again." onRetry={reload} />}
      {state.status === 'ready' && (
        <>
          <Card className="card-pad-table orders-card">
            {rows.length === 0 ? (
              all.length === 0
                ? <EmptyState title="No orders yet" body="New WhatsApp orders will appear here." action={<Link to="/app/journey" className="btn btn--dark btn--md">Open Journey player</Link>} />
                : <EmptyState title="No matching orders" body="Try another status, payment or search." action={<Button onClick={() => { setTab('All'); setPay(null); setQ('') }}>Clear filters</Button>} />
            ) : (
              <>
                <div className="tbl-scroll">
                  <table className="tbl orders-table">
                    <thead><tr><th>Order</th><th>Customer</th><th>Items</th><th>Total</th><th>Order status</th><th>Payment</th><th>Expires</th></tr></thead>
                    <tbody>
                      {view.map((o) => {
                        const exp = expiryOf(o, now)
                        const negotiated = o.items.some((i) => i.negotiatedPrice !== undefined)
                        return (
                          <tr key={o.id} className="is-link" onClick={() => navigate(`/app/orders/${o.id}`)}>
                            <td><Link to={`/app/orders/${o.id}`} className="cell-link" onClick={(e) => e.stopPropagation()}><Mono>{o.number}</Mono></Link>
                              <span className="sub">{shortDate(o.createdAt)}{o.channel ? ` · ${o.channel}` : ''}</span></td>
                            <td><span className="strong">{o.customerName}</span><span className="sub">{maskPhone(o.customerPhone)}</span></td>
                            <td className="items">{o.items.map((i) => `${i.name} × ${i.qty}`).join(', ')}</td>
                            <td className="total">{lkr(o.total)}{negotiated && <><br /><Chip tone="violet">Negotiated</Chip></>}</td>
                            <td><StatusBadge status={o.status} /></td>
                            <td><StatusBadge status={o.payment} /></td>
                            <td className={exp.tone === 'dash' ? 'dim' : `exp--${exp.tone}`}>{exp.text}</td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                </div>
                <div className="table-foot">
                  <span>Showing {page * PAGE + 1}–{page * PAGE + view.length} of {rows.length}</span>
                  <div className="table-foot__nav">
                    <Button variant="outline" size="sm" disabledReason={page === 0 ? 'This is the first page' : undefined} onClick={() => setPage(page - 1)}>Previous</Button>
                    <Button variant="white" size="sm" disabledReason={page >= pages - 1 ? 'This is the last page' : undefined} onClick={() => setPage(page + 1)}>Next</Button>
                  </div>
                </div>
              </>
            )}
          </Card>

          {/* Mobile (MobileOrders sheet): cards instead of a table */}
          <ul className="order-cards" aria-label="Orders">
            {rows.slice(0, (page + 1) * PAGE).map((o) => (
              <li key={o.id}>
                <Link to={`/app/orders/${o.id}`} className="ocard">
                  <span className="ocard__top"><Mono className="ocard__no">{o.number}</Mono><span className="ocard__total">{lkr(o.total)}</span></span>
                  <span className="ocard__name" style={{ display: 'block' }}>{o.customerName}</span>
                  <span className="ocard__badges"><StatusBadge status={o.status} /><StatusBadge status={o.payment} /></span>
                </Link>
              </li>
            ))}
            {rows.length > (page + 1) * PAGE && <li><Button variant="dark" onClick={() => setPage(page + 1)} style={{ width: '100%' }}>Show more</Button></li>}
          </ul>
        </>
      )}
    </>
  )
}
