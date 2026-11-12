import { useState } from 'react'
import { data } from '../data'
import type { Conversation } from '../data/types'
import { ChatMessageView } from '../ui/Chat'
import { useAsync } from '../hooks/useAsync'
import { maskPhone } from '../lib/format'
import { Avatar, ButtonLink, Card, InlineError, PageHeader, Skeleton } from '../ui/primitives'

export default function Inbox() {
  const [state, reload] = useAsync(() => data.conversations(), [])
  const [activeId, setActiveId] = useState(1)

  return (
    <>
      <PageHeader eyebrow="WhatsApp" title="Inbox" />
      {state.status === 'loading' && <Card><Skeleton /></Card>}
      {state.status === 'error' && <InlineError title="Could not load conversations" body="Check your connection and try again." onRetry={reload} />}
      {state.status === 'ready' && <InboxBody convs={state.data.data} activeId={activeId} setActiveId={setActiveId} />}
    </>
  )
}

function InboxBody({ convs, activeId, setActiveId }: { convs: Conversation[]; activeId: number; setActiveId: (id: number) => void }) {
  const c = convs.find((x) => x.id === activeId) ?? convs[0]
  return (
    <div className="inbox">
      <Card className="convs" aria-label="Conversations">
        <ul role="listbox" aria-label="Conversations">
          {convs.map((x) => (
            <li key={x.id}>
              <button type="button" role="option" aria-selected={x.id === c.id} className={`conv${x.id === c.id ? ' is-active' : ''}`} onClick={() => setActiveId(x.id)}>
                <Avatar name={x.name} size="sm" />
                <span className="conv__body">
                  <span className="conv__name">{x.name}<span className="conv__time">{x.time}</span></span>
                  <span className="conv__preview" lang={x.previewLang} style={{ display: 'block' }}>{x.preview}</span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      </Card>

      <Card className="chat" aria-label={`Conversation with ${c.name}`}>
        <div className="chat__head">
          <Avatar name={c.name} size="sm" />
          <div className="chat__who"><p className="chat__name">{c.name}</p><p className="chat__phone">{maskPhone(c.phone)}</p></div>
          {c.orderId && <ButtonLink to={`/app/orders/${c.orderId}`} variant="dark">Open order</ButtonLink>}
        </div>
        <div className="chat__body" aria-live="polite">
          <p className="sim-pill">Simulated conversation · transcripts are not stored</p>
          {c.messages.map((m, i) => <ChatMessageView key={i} m={m} />)}
        </div>
        <p className="chat__foot">Read-only. Replies are sent by the bot, not typed here.</p>
      </Card>

      <Card className="analysis" aria-labelledby="ai-t">
        <h2 id="ai-t" className="card__title">AI analysis · last message</h2>
        <dl className="kv" style={{ marginTop: 12 }}>
          {[['Language', c.analysis.language], ['Intent', c.analysis.intent], ['Confidence', c.analysis.confidence.toFixed(2)], ['Product match', c.analysis.productMatch], ['Decided by', c.analysis.decidedBy]].map(([k, v]) => (
            <div className="kv__row" key={k}><dt>{k}</dt><dd>{v}</dd></div>
          ))}
        </dl>
        <p className="note" style={{ marginTop: 14 }}>AI labels the message. Price, stock and payment are decided by code.</p>
      </Card>
    </div>
  )
}
