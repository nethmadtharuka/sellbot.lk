import { useCallback, useEffect, useMemo, useState } from 'react'
import { DEMO_NOW } from '../data/demo'
import { data } from '../data'
import { useAsync } from '../hooks/useAsync'
import { lkr } from '../lib/format'
import { Button, Card, ConfirmModal, EmptyState, Field, InlineError, Mono, PageHeader, Skeleton, Toast } from '../ui/primitives'

function receivedLabel(iso: string | undefined, now: Date): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (d.toDateString() === now.toDateString()) return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
  const y = new Date(now); y.setDate(now.getDate() - 1)
  return d.toDateString() === y.toDateString() ? 'Yesterday' : d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })
}

export default function Payments() {
  const [state, reload] = useAsync(() => data.orders(), [])
  const now = data.mode === 'demo' ? DEMO_NOW : new Date()
  const queue = useMemo(() => (state.status === 'ready' ? state.data.data : [])
    .filter((o) => o.payment === 'Pending review')
    .sort((a, b) => (b.slipReceived ?? '').localeCompare(a.slipReceived ?? '')), [state])
  const [sel, setSel] = useState<number | null>(null)
  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState('')
  const [toast, setToast] = useState('')
  const [error, setError] = useState('')
  const [slip, setSlip] = useState<string | null>(null)
  const clear = useCallback(() => setToast(''), [])
  const current = queue.find((o) => o.id === sel) ?? queue[0]

  useEffect(() => {
    if (!current) return
    let url: string | null = null
    void data.slipImage(current.id).then((u) => { url = u; setSlip(u) })
    return () => { if (url) URL.revokeObjectURL(url); setSlip(null) }
  }, [current])

  async function decide(approve: boolean) {
    if (!current) return
    setError('')
    try {
      if (approve) await data.approvePayment(current.id)
      else await data.rejectPayment(current.id, reason.trim() || undefined)
      setToast(approve ? 'Payment approved. Order is now Paid.' : 'Payment rejected. Order is back to Unpaid.')
      setSel(null); setReason(''); reload()
    } catch (e) { setError(e instanceof Error ? e.message : 'The action failed.') }
  }

  return (
    <>
      <PageHeader eyebrow="Finance" title="Payments" />
      {state.status === 'loading' && <Card><Skeleton /></Card>}
      {state.status === 'error' && <InlineError title="Could not load payments" body="Check your connection and try again." onRetry={reload} />}
      {state.status === 'ready' && (
        <div className="pay-grid">
          <Card className="card-pad" aria-labelledby="q-t">
            <h2 id="q-t" className="card__title">Slips to review <span className="muted" style={{ fontWeight: 400 }}>({queue.length})</span></h2>
            {queue.length === 0 ? <EmptyState title="All caught up" body="No payment slips are waiting for review." /> : (
              <div className="tbl-scroll">
                <table className="tbl pay-list" style={{ marginTop: 6 }}>
                  <thead><tr><th>Order</th><th>Customer</th><th>Amount</th><th>Received</th></tr></thead>
                  <tbody>
                    {queue.map((o) => (
                      <tr key={o.id} aria-selected={o.id === current?.id} onClick={() => setSel(o.id)}>
                        <td><button type="button" className="cell-link" onClick={() => setSel(o.id)} aria-label={`Review ${o.number}`}><Mono>{o.number}</Mono></button></td>
                        <td style={{ color: 'var(--c-text)' }}>{o.customerName}</td>
                        <td style={{ color: 'var(--c-text)' }}>{lkr(o.total)}</td>
                        <td style={{ color: 'var(--c-text)' }}>{receivedLabel(o.slipReceived, now)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
          {current && (
            <Card className="card-pad review" aria-labelledby="r-t">
              <h2 id="r-t" className="card__title">{current.number}</h2>
              {slip ? <img className="slip-img" src={slip} alt={`Bank slip for ${current.number}`} /> : <div className="slip-ph">Bank slip image (placeholder)</div>}
              <div className="review__facts">
                <p>Expected <b>{lkr(current.total)}</b></p>
                <p>Customer {current.customerName}</p>
              </div>
              {error && <p className="login-card__error" role="alert">{error}</p>}
              <div className="review__actions">
                <Button variant="green" onClick={() => void decide(true)}>Approve → Paid</Button>
                <Button variant="danger" onClick={() => setRejecting(true)}>Reject</Button>
              </div>
            </Card>
          )}
        </div>
      )}
      <ConfirmModal open={rejecting} title={`Reject the slip for ${current?.number ?? ''}?`}
        body="The order returns to Unpaid and the customer is asked to send the slip again."
        confirmLabel="Reject slip" cancelLabel="Keep slip" onClose={() => setRejecting(false)}
        onConfirm={() => { setRejecting(false); void decide(false) }}>
        <Field label="Reason (optional)">{(fid) => <input id={fid} className="input" value={reason} onChange={(e) => setReason(e.target.value)} maxLength={200} />}</Field>
      </ConfirmModal>
      {toast && <div className="toast-host"><Toast message={toast} onDone={clear} /></div>}
    </>
  )
}
