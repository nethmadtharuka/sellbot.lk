import { Link, useParams } from 'react-router-dom'
import { data } from '../data'
import { useAsync } from '../hooks/useAsync'
import { compactK, lkr, maskPhone } from '../lib/format'
import { Avatar, ButtonLink, Card, Chip, InlineError, Mono, PageHeader, Skeleton, StatusBadge } from '../ui/primitives'

const LANG_NAME = { en: 'English', si: 'Sinhala', ta: 'Tamil' } as const
const sinceLabel = (iso: string) => new Date(iso).toLocaleString('en-GB', { month: 'short', year: 'numeric' })

export default function CustomerDetail() {
  const id = Number(useParams().id)
  const [state, reload] = useAsync(() => data.customer(id), [id])
  const back = <ButtonLink to="/app/customers" variant="dark"><span aria-hidden="true">←</span> Back</ButtonLink>

  if (state.status === 'loading') return <><PageHeader crumb="Customers" title="Customer" right={back} /><Card><Skeleton /></Card></>
  const found = state.status === 'ready' ? state.data.data : null
  if (!found) {
    return <><PageHeader crumb="Customers" title="Customer not found" right={back} />
      <InlineError title="Could not load this customer" body="They may not exist in this dataset." onRetry={reload} /></>
  }
  const { customer: c, orders } = found
  const open = orders.filter((o) => o.status !== 'Delivered' && o.status !== 'Cancelled').length
  const avg = c.orders ? c.lifetime / c.orders : 0

  return (
    <>
      <PageHeader crumb={<><Link to="/app/customers">Customers</Link> / {c.name}</>} title={c.name} right={back} />
      <div className="cd-grid">
        <Card className="cd-profile" aria-label="Profile">
          <div className="cd-id">
            <Avatar name={c.name} size="lg" />
            <div>
              <p className="cd-id__name">{c.name}</p>
              <p className="note">{maskPhone(c.phone)}</p>
              <p className="cd-id__chips"><Chip>{LANG_NAME[c.lang]}</Chip><Chip>Customer since {sinceLabel(c.since)}</Chip></p>
            </div>
          </div>
          <dl className="stat-tiles">
            {[['Orders', String(c.orders)], ['Lifetime', compactK(c.lifetime)], ['Avg order', compactK(avg)], ['Open', String(open)]].map(([k, v]) => (
              <div className="stat-tile" key={k}><dt className="stat-tile__label">{k}</dt><dd className="stat-tile__value" style={{ margin: 0 }}>{v}</dd></div>
            ))}
          </dl>
        </Card>
        <Card className="card-pad" aria-labelledby="oh-t">
          <h2 id="oh-t" className="card__title">Order history</h2>
          <div className="tbl-scroll">
            <table className="tbl" style={{ marginTop: 6 }}>
              <thead><tr><th>Order</th><th>Total</th><th>Status</th><th>Payment</th></tr></thead>
              <tbody>
                {orders.map((o) => (
                  <tr key={o.id}>
                    <td><Link className="cell-link" to={`/app/orders/${o.id}`}><Mono>{o.number}</Mono></Link></td>
                    <td style={{ color: 'var(--c-text)' }}>{lkr(o.total)}</td>
                    <td><StatusBadge status={o.status} /></td>
                    <td><StatusBadge status={o.payment} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="note" style={{ marginTop: 14 }}>Chat transcripts are not stored, so no message history is shown.</p>
        </Card>
      </div>
    </>
  )
}
