import { useEffect, useState } from 'react'
import type { ChatMessage } from '../data/types'
import type { OrderStatus, PaymentStatus } from '../lib/status'
import { Icon } from '../ui/Icon'
import { ChatMessageView } from '../ui/Chat'
import { Button, Card, PageHeader, StatusBadge } from '../ui/primitives'

type Frame = { step: string; chat: ChatMessage[]; stock: string; order: OrderStatus | null; payment: PaymentStatus | null; expires: string }

/** Scripted replay of the real pipeline for ORD-2026-041 (fictional). */
const ask: ChatMessage = { kind: 'in', text: 'chair ekak denna' }
const ai: ChatMessage = { kind: 'ai', lang: 'SI', intent: 'Order', confidence: 0.96 }
const confirmed: ChatMessage = { kind: 'out', title: 'Order Confirmed!', lines: ['ORD-2026-041 · Total LKR 28,500'] }
const created: ChatMessage = { kind: 'event', text: 'Order created · stock 20 → 19', tone: 'green' }
const slip: ChatMessage = { kind: 'in', text: 'Slip sent' }
const slipAck: ChatMessage = { kind: 'out', title: 'Payment slip received', lines: ['Our team will verify and confirm shortly.'] }
const review: ChatMessage = { kind: 'event', text: 'Payment → Pending review', tone: 'blue' }
const paid: ChatMessage = { kind: 'out', title: 'Payment confirmed!', lines: ['Invoice SellBotLk-ORD-2026-041.pdf sent.'] }
const delivered: ChatMessage = { kind: 'out', title: 'Delivered', lines: ['Your order ORD-2026-041 has been delivered.'] }

const FRAMES: Frame[] = [
  { step: 'Customer asks', chat: [ask], stock: '20', order: null, payment: null, expires: '—' },
  { step: 'AI classifies', chat: [ask, ai], stock: '20', order: null, payment: null, expires: '—' },
  { step: 'Code checks stock', chat: [ask, ai, { kind: 'event', text: 'Stock check · 20 available', tone: 'green' }], stock: '20', order: null, payment: null, expires: '—' },
  { step: 'Order created', chat: [ask, confirmed, created], stock: '20 → 19', order: 'Confirmed', payment: 'Unpaid', expires: '24 h' },
  { step: 'Slip uploaded', chat: [ask, confirmed, slip, slipAck, review], stock: '19', order: 'Confirmed', payment: 'Pending review', expires: 'Paused for review' },
  { step: 'Owner approves', chat: [slip, slipAck, paid], stock: '19', order: 'Confirmed', payment: 'Paid', expires: '—' },
  { step: 'Delivered', chat: [paid, delivered], stock: '19', order: 'Delivered', payment: 'Paid', expires: '—' },
]
const STEP_MS = 2200

export default function Journey() {
  const [i, setI] = useState(3) // the design shows step 4 of 7
  const [playing, setPlaying] = useState(false)
  useEffect(() => {
    if (!playing) return
    const t = setTimeout(() => { if (i >= FRAMES.length - 1) setPlaying(false); else setI(i + 1) }, STEP_MS)
    return () => clearTimeout(t)
  }, [playing, i])
  const f = FRAMES[i]

  return (
    <>
      <PageHeader eyebrow="AI" title="Order journey" right={<Button variant="dark" onClick={() => { setPlaying(false); setI(0) }}>Restart</Button>} />
      <div className="jr-grid">
        <Card className="jsteps" aria-label="Steps">
          <ol>
            {FRAMES.map((fr, n) => (
              <li key={fr.step}>
                <button type="button" className={`jstep${n < i ? ' is-done' : ''}${n === i ? ' is-done is-current' : ''}`} aria-current={n === i ? 'step' : undefined}
                  onClick={() => { setPlaying(false); setI(n) }}>
                  <span className="jstep__n">{n + 1}</span>{fr.step}
                </button>
              </li>
            ))}
          </ol>
        </Card>
        <Card className="jplayer" aria-label="Replay">
          <div className="jplayer__chat" aria-live="polite">{f.chat.map((m, n) => <ChatMessageView key={`${i}-${n}`} m={m} />)}</div>
          <div className="jplayer__bar">
            <button type="button" className="play-btn" aria-label={playing ? 'Pause' : 'Play'} onClick={() => { if (i >= FRAMES.length - 1) setI(0); setPlaying(!playing) }}>
              {playing ? <span aria-hidden="true" style={{ fontWeight: 700 }}>❚❚</span> : <Icon name="play" size={18} />}
            </button>
            <div className="progress" role="progressbar" aria-valuemin={1} aria-valuemax={FRAMES.length} aria-valuenow={i + 1} aria-label="Journey progress">
              <div className="progress__fill" style={{ width: `${((i + 1) / FRAMES.length) * 100}%` }} />
            </div>
            <span>Step {i + 1} of {FRAMES.length}</span>
          </div>
        </Card>
        <Card className="sysstate" aria-labelledby="ss-t">
          <h2 id="ss-t" className="card__title" style={{ marginBottom: 10 }}>System state</h2>
          <div className="sysstate__row"><p className="sysstate__label">Stock</p><p className="sysstate__value">{f.stock}</p></div>
          <div className="sysstate__row"><p className="sysstate__label">Order</p><p className="sysstate__value">{f.order ? <StatusBadge status={f.order} /> : '—'}</p></div>
          <div className="sysstate__row"><p className="sysstate__label">Payment</p><p className="sysstate__value">{f.payment ? <StatusBadge status={f.payment} /> : '—'}</p></div>
          <div className="sysstate__row"><p className="sysstate__label">Expires</p><p className="sysstate__value">{f.expires}</p></div>
        </Card>
      </div>
    </>
  )
}
