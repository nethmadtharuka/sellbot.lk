import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { data } from '../data'
import { useAsync } from '../hooks/useAsync'
import { lkr, maskPhone, shortDate } from '../lib/format'
import { Avatar, Card, Chip, EmptyState, InlineError, LangChip, Mono, PageHeader, SearchField, Skeleton } from '../ui/primitives'

export default function Customers() {
  const [state, reload] = useAsync(() => data.customers(), [])
  const [q, setQ] = useState('')
  const navigate = useNavigate()
  const rows = useMemo(() => {
    const all = state.status === 'ready' ? state.data.data : []
    const n = q.trim().toLowerCase()
    return n ? all.filter((c) => c.name.toLowerCase().includes(n) || c.phone.includes(n.replace(/\D/g, '') || '§')) : all
  }, [state, q])
  const simulatedInLive = data.mode === 'live' && state.status === 'ready' && state.data.simulated

  return (
    <>
      <div className="cust-head">
        <PageHeader eyebrow="People" title={<>Customers{simulatedInLive && <span className="sim-tag"><Chip tone="violet">Simulated · no customers API</Chip></span>}</>}
          right={<SearchField label="Search customers" placeholder="Name or phone" value={q} onChange={setQ} />} />
      </div>
      {state.status === 'loading' && <Card><Skeleton /></Card>}
      {state.status === 'error' && <InlineError title="Could not load customers" body="Check your connection and try again." onRetry={reload} />}
      {state.status === 'ready' && (
        <Card className="card-pad-table">
          {rows.length === 0 ? <EmptyState title="No matching customers" body="Try another name or phone number." /> : (
            <div className="tbl-scroll">
              <table className="tbl cust-table">
                <thead><tr><th>Customer</th><th>Phone</th><th>Language</th><th>Orders</th><th>Lifetime</th><th>Last order</th></tr></thead>
                <tbody>
                  {rows.map((c) => (
                    <tr key={c.id} className="is-link" onClick={() => navigate(`/app/customers/${c.id}`)}>
                      <td><Link to={`/app/customers/${c.id}`} className="cust-cell" onClick={(e) => e.stopPropagation()}><Avatar name={c.name} />{c.name}</Link></td>
                      <td><Mono>{maskPhone(c.phone)}</Mono></td>
                      <td><LangChip lang={c.lang} variant="native" /></td>
                      <td style={{ color: 'var(--c-text)' }}>{c.orders}</td>
                      <td style={{ color: 'var(--c-text)' }}>{lkr(c.lifetime)}</td>
                      <td style={{ color: 'var(--c-text)' }}>{shortDate(c.lastOrder)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      )}
    </>
  )
}
