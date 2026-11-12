import { useState } from 'react'
import { NEVER_SET_STATUSES, ORDER_STATUSES, PAYMENT_STATUSES } from '../lib/status'
import { Button, ButtonLink, Card, EmptyState, InlineError, ModeBanner, Skeleton, StatusBadge, Toast } from '../ui/primitives'

/** The "UI states" sheet: every shared state, rendered by the real components. */
export default function States() {
  const [retries, setRetries] = useState(0)
  return (
    <main className="states" id="main">
      <p className="eyebrow">Phase 1 · Foundation</p>
      <h1 className="states__title">UI states</h1>
      <div className="states__grid">
        <div>
          <p className="eyebrow states__label">LOADING · skeleton</p>
          <Card><Skeleton /></Card>
          <p className="eyebrow states__label">EMPTY</p>
          <Card><EmptyState title="No orders yet" body="New WhatsApp orders will appear here." action={<ButtonLink to="/app/journey" variant="dark">Open Journey player</ButtonLink>} /></Card>
          <p className="eyebrow states__label">ERROR · inline</p>
          <InlineError title="Could not load orders" body={retries ? `Retried ${retries} time${retries > 1 ? 's' : ''}. Check your connection and try again.` : 'Check your connection and try again.'} onRetry={() => setRetries(retries + 1)} />
        </div>
        <div>
          <p className="eyebrow states__label">SUCCESS · toast + DISABLED</p>
          <Toast message="Payment approved. Order is now Paid." onDone={() => undefined} />
          <div className="states__row">
            <Button variant="green" disabledReason="Disabled in demo">Approve payment</Button>
            <Button variant="dark" disabledReason="Disabled in demo">Track order</Button>
            <span>Disabled in demo</span>
          </div>
          <p className="eyebrow states__label">CONFIRM · modal</p>
          <div className="modal" role="group" aria-label="Confirm modal example">
            <h2 className="modal__title">Cancel order ORD-2026-039?</h2>
            <p className="modal__body">Stock for 2 items will be returned and the customer is notified on WhatsApp.</p>
            <div className="modal__actions"><Button variant="danger">Cancel order</Button><Button variant="dark">Keep order</Button></div>
          </div>
          <p className="eyebrow states__label">DEMO vs LIVE banner</p>
          <div className="stack" style={{ gap: 10 }}><ModeBanner mode="demo" /><ModeBanner mode="live" /></div>
          <p className="eyebrow states__label">STATUS BADGES · dashed = never set by backend code</p>
          <div className="states__badges">
            {[...ORDER_STATUSES, ...PAYMENT_STATUSES.filter((p) => p !== 'Paid'), 'Paid' as const, ...NEVER_SET_STATUSES].map((s) => <StatusBadge key={s} status={s} />)}
          </div>
          <p className="eyebrow states__label">SINHALA · TAMIL · LONG NAMES</p>
          <Card className="card-pad">
            <p lang="si" style={{ fontSize: 'var(--fs-16)' }}>මට පුටුවක් ඕනේ. මිල කීයද?</p>
            <p lang="ta" style={{ fontSize: 'var(--fs-16)' }}>எனக்கு ஒரு சோபா வேண்டும்.</p>
            <p style={{ marginTop: 6, maxWidth: 272, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }} title="Thilini Wijesundara Perera Gunawardena">Thilini Wijesundara Perera Gunawardena</p>
          </Card>
        </div>
      </div>
    </main>
  )
}
