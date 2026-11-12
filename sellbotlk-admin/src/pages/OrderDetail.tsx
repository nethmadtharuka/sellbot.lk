import { useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { data } from '../data'
import { demoCustomers } from '../data/demo'
import { useAsync } from '../hooks/useAsync'
import { lkr, maskPhone, shortDate } from '../lib/format'
import { FULFILMENT_STEPS, nextFulfilmentStatus, type OrderStatus } from '../lib/status'
import { Icon } from '../ui/Icon'
import { Button, ButtonLink, Card, Chip, ConfirmModal, Field, InlineError, PageHeader, Skeleton, StatusBadge, Toast } from '../ui/primitives'

const LANG_NAME = { en: 'English', si: 'Sinhala', ta: 'Tamil' } as const

function received(iso?: string): string {
  if (!iso) return '—'
  const d = new Date(iso)
  return `${d.getDate()} ${d.toLocaleString('en-GB', { month: 'short' })}, ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

export default function OrderDetail() {
  const id = Number(useParams().id)
  const [state, reload] = useAsync(() => data.order(id), [id])
  const [confirm, setConfirm] = useState<'advance' | 'reject' | null>(null)
  const [reason, setReason] = useState('')
  const [toast, setToast] = useState('')
  const [error, setError] = useState('')
  const [slipUrl, setSlipUrl] = useState<string | null>(null)

  const order = state.status === 'ready' ? state.data.data : null
  const next: OrderStatus | '' = order ? nextFulfilmentStatus(order.status) ?? '' : ''
  useEffect(() => {
    if (!order || order.payment !== 'Pending review') return
    let url: string | null = null
    void data.slipImage(order.id).then((u) => { url = u; setSlipUrl(u) })
    return () => { if (url) URL.revokeObjectURL(url) }
  }, [order])
  const clearToast = useCallback(() => setToast(''), [])

  async function run(action: () => Promise<void>, done: string) {
    setError('')
    try { await action(); setToast(done); reload() } catch (e) { setError(e instanceof Error ? e.message : 'The action failed.') }
  }

  if (state.status === 'loading') return <><PageHeader crumb="Orders" title="Order" /><Card><Skeleton /></Card></>
  if (state.status === 'error' || !order) {
    return <><PageHeader crumb="Orders" title="Order not found" right={<ButtonLink to="/app/orders" variant="dark"><Icon name="arrow-left" size={16} />Back to orders</ButtonLink>} />
      <InlineError title="Could not load this order" body={state.status === 'error' ? state.error : 'It may have been removed.'} onRetry={reload} /></>
  }

  const stepIndex = FULFILMENT_STEPS.indexOf(order.status)
  const canAdvance = Boolean(next)
  const customer = demoCustomers.find((c) => c.id === order.customerId)
  const neg = order.items.find((i) => i.negotiatedPrice !== undefined)

  return (
    <>
      <PageHeader
        crumb={<><Link to="/app/orders">Orders</Link> / {order.number}</>}
        title={order.number}
        right={<ButtonLink to="/app/orders" variant="dark"><span aria-hidden="true">←</span> Back to orders</ButtonLink>}
      />
      <div className="od-meta">
        <StatusBadge status={order.status} /><StatusBadge status={order.payment} />
        <span>{shortDate(order.createdAt)}{order.channel ? ` · ${order.channel}` : ''}</span>
      </div>
      {error && <div style={{ marginBottom: 16 }}><InlineError title="Action failed" body={error} /></div>}

      <div className="od-grid-1">
        <Card className="card-pad" aria-labelledby="ful-t">
          <h2 id="ful-t" className="card__title">Fulfilment</h2>
          <ol className="stepper" aria-label="Fulfilment progress">
            {FULFILMENT_STEPS.map((s, i) => {
              const done = order.status !== 'Cancelled' && i <= stepIndex
              return (
                <li key={s} className={`step${done ? ' is-done' : ''}`} aria-current={i === stepIndex ? 'step' : undefined}>
                  <span className="step__dot">{done && <Icon name="check" size={16} strokeWidth={2.5} />}</span>{s}
                </li>
              )
            })}
          </ol>
          <Field label="Next status">{(fid) => (
            <div className="next-row">
              <select id={fid} className="select" value={next} disabled={!canAdvance} onChange={() => undefined}>
                {canAdvance ? <option value={next}>{next}</option> : <option value="">No further status</option>}
              </select>
              <Button variant="green" disabledReason={canAdvance ? undefined : 'This order has no further fulfilment step'} onClick={() => setConfirm('advance')}>Update status</Button>
            </div>
          )}</Field>
          <p className="note" style={{ marginTop: 14 }}>{canAdvance ? `Moving to ${next} sends the customer a WhatsApp message.` : 'Fulfilment is complete or the order is cancelled.'}</p>
        </Card>

        <Card className="card-pad" aria-labelledby="items-t">
          <h2 id="items-t" className="card__title">Items</h2>
          {order.items.map((i) => (
            <div className="od-items__line" key={i.name}><span>{i.name} × {i.qty}</span><span>{lkr((i.negotiatedPrice ?? i.unitPrice) * i.qty)}</span></div>
          ))}
          {neg && (
            <p className="neg-note">Negotiated. List {neg.unitPrice.toLocaleString('en-US')}{neg.minPrice !== undefined ? ` · min ${neg.minPrice.toLocaleString('en-US')}` : ''} · accepted {neg.negotiatedPrice?.toLocaleString('en-US')} (≥ min price).</p>
          )}
          <div className="od-total"><span>Total</span><span>{lkr(order.total)}</span></div>
        </Card>
      </div>

      <div className="od-grid-2">
        <Card className="card-pad" aria-labelledby="pay-t">
          <h2 id="pay-t" className="card__title">Payment · bank slip</h2>
          <div className="pay-body">
            {slipUrl ? <img className="slip-img" src={slipUrl} alt={`Payment slip for ${order.number}`} /> : <div className="slip-ph">Slip image<br />(placeholder)</div>}
            <div className="pay-facts">
              <p>Status <StatusBadge status={order.payment} /></p>
              <p>Amount due {lkr(order.total)}</p>
              <p>Received {received(order.slipReceived)}</p>
              {order.payment === 'Pending review' ? (
                <div className="pay-actions">
                  <Button variant="green" className="btn--round btn--approve-narrow" onClick={() => void run(() => data.approvePayment(order.id), 'Payment approved. Order is now Paid.')}>Approve payment</Button>
                  <Button variant="danger" className="btn--round" onClick={() => setConfirm('reject')}>Reject</Button>
                </div>
              ) : (
                <div className="pay-actions">
                  <Button variant="green" className="btn--round btn--approve-narrow" disabledReason="No slip is waiting for review">Approve payment</Button>
                  <Button variant="danger" className="btn--round" disabledReason="No slip is waiting for review">Reject</Button>
                </div>
              )}
            </div>
          </div>
        </Card>

        <Card className="card-pad" aria-labelledby="cust-t">
          <h2 id="cust-t" className="card__title">Customer</h2>
          <p className="cust-mini__name">{order.customerId && customer ? <Link className="cell-link" to={`/app/customers/${order.customerId}`}>{order.customerName}</Link> : order.customerName}</p>
          <p className="note">{maskPhone(order.customerPhone)}</p>
          <div className="cust-mini__chips">
            {customer && <Chip>{LANG_NAME[customer.lang]}</Chip>}
            {customer && <Chip>{customer.orders} orders</Chip>}
          </div>
        </Card>

        <Card className="card-pad" aria-labelledby="prev-t">
          <h2 id="prev-t" className="card__title">Customer message preview</h2>
          <p className="preview-bubble">Your order {order.number} is {order.status.toLowerCase()}.</p>
          <div className="preview-actions">
            <Button variant="dark" disabledReason="Disabled in demo: button replies are not handled by the bot yet">Track order</Button>
            <Button variant="dark" disabledReason="Disabled in demo: button replies are not handled by the bot yet">Cancel order</Button>
          </div>
          <p className="note" style={{ marginTop: 10, fontSize: 'var(--fs-12)' }}>Buttons are disabled in the demo.</p>
        </Card>
      </div>

      <ConfirmModal open={confirm === 'advance'} danger={false} title={`Move ${order.number} to ${next}?`}
        body={data.mode === 'live' ? `The customer is notified on WhatsApp.` : 'Simulated: no WhatsApp message is sent in the demo.'}
        confirmLabel="Update status" cancelLabel="Keep status" onClose={() => setConfirm(null)}
        onConfirm={() => { setConfirm(null); if (next) void run(() => data.advanceOrder(order.id, next), `Order moved to ${next}.`) }} />
      {confirm === 'reject' && (
        <RejectDialog number={order.number} reason={reason} setReason={setReason} onClose={() => setConfirm(null)}
          onConfirm={() => { setConfirm(null); void run(() => data.rejectPayment(order.id, reason.trim() || undefined), 'Payment rejected. Order is back to Unpaid.') }} />
      )}
      {toast && <div className="toast-host"><Toast message={toast} onDone={clearToast} /></div>}
    </>
  )
}

/** Reject needs an optional reason; it reuses the modal styling from the States sheet. */
function RejectDialog({ number, reason, setReason, onClose, onConfirm }: { number: string; reason: string; setReason: (v: string) => void; onClose: () => void; onConfirm: () => void }) {
  return (
    <ConfirmModal open title={`Reject the slip for ${number}?`}
      body={`The order returns to Unpaid and the customer is asked to send the slip again.${reason ? ` Reason: ${reason}` : ''}`}
      confirmLabel="Reject slip" cancelLabel="Keep slip" onClose={onClose} onConfirm={onConfirm}>
      <Field label="Reason (optional)">{(fid) => <input id={fid} className="input" value={reason} onChange={(e) => setReason(e.target.value)} maxLength={200} />}</Field>
    </ConfirmModal>
  )
}
