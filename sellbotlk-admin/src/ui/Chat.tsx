import type { ChatMessage } from '../data/types'
import { Chip } from './primitives'

/** Renders **bold** segments used in the scripted bot replies. */
function rich(text: string) {
  return text.split(/(\*\*[^*]+\*\*)/).map((part, i) => (part.startsWith('**') ? <b key={i}>{part.slice(2, -2)}</b> : part))
}

export function ChatMessageView({ m }: { m: ChatMessage }) {
  switch (m.kind) {
    case 'in': return <p className="bubble bubble--in" lang={m.lang}>{m.text}</p>
    case 'out': return (
      <div className="bubble bubble--out">
        {m.title && <p className="bubble__title">{m.title}</p>}
        {m.lines.map((l, i) => <p key={i}>{rich(l)}</p>)}
      </div>
    )
    case 'event': return <p className={`event event--${m.tone}`}>{m.text}</p>
    case 'ai': return <p className="ai-chips" aria-label={`AI: language ${m.lang}, intent ${m.intent}, confidence ${m.confidence}`}><Chip>{m.lang}</Chip><Chip tone="violet">{m.intent} · {m.confidence.toFixed(2)}</Chip></p>
  }
}
