import { useMemo, useState } from 'react'
import { demoProducts } from '../data/demo'
import { lkr } from '../lib/format'
import { decideOffer, decisionLabel } from '../lib/negotiation'
import { Button, Card, Field, PageHeader } from '../ui/primitives'

type Preset = 'Sinhala' | 'Tamil' | 'English' | 'Negotiation'
const PRESETS: Record<Preset, string> = {
  Sinhala: 'chair ekak denna',
  Tamil: 'எனக்கு sofa வேண்டும்',
  English: 'Do you have a study desk?',
  Negotiation: 'chair ekak denna, 25000 walata denna puluwanda?',
}

type Classification = { language: string; intent: string; product: string | null; offer: number | null; confidence: number }

/**
 * SCRIPTED classifier for the demo. No Gemini call is made: the backend has no
 * side-effect-free classification endpoint, so this mimics the shape of its output.
 */
function classify(text: string): Classification {
  const t = text.toLowerCase()
  const language = /[඀-෿]/.test(text) ? 'si' : /[஀-௿]/.test(text) ? 'ta'
    : /\b(denna|ekak|walata|puluwanda|eka|kiyada)\b/.test(t) ? 'si-romanised' : 'en'
  const product = /chair/.test(t) ? 'Executive Chair' : /sofa/.test(t) ? 'Three-Seater Fabric Sofa' : /book ?shelf/.test(t) ? 'Wooden Bookshelf (5-tier)'
    : /table/.test(t) ? 'Dining Table Set' : /desk/.test(t) ? 'Study Desk' : null
  const num = text.replace(/,/g, '').match(/\b(\d{4,7})\b/)
  const offer = num ? Number(num[1]) : null
  const intent = offer ? 'negotiate' : /\b(denna|want|order|buy|வேண்டும்)\b|දෙන්න/.test(t) && product ? 'order' : product ? 'product_search' : 'other'
  const confidence = intent === 'other' ? 0.41 : offer ? 0.93 : language === 'ta' ? 0.94 : 0.96
  return { language, intent, product, offer, confidence }
}

export default function Playground() {
  const [preset, setPreset] = useState<Preset>('Negotiation')
  const [text, setText] = useState(PRESETS.Negotiation)
  const [result, setResult] = useState<Classification>(() => classify(PRESETS.Negotiation))
  const product = useMemo(() => demoProducts.find((p) => result.product && p.name.startsWith(result.product.split(' ')[0])), [result])

  const json = JSON.stringify({ language: result.language, intent: result.intent, product: result.product, ...(result.offer ? { offer: result.offer } : {}), confidence: result.confidence }, null, 2)
    .replace(/"(\w+)": /g, '"$1": ')
  const decision = product && result.offer && product.minPrice !== undefined ? decideOffer(product.price, product.minPrice, result.offer) : null

  return (
    <>
      <PageHeader eyebrow="AI" title="AI Playground" />
      <div className="pg-grid">
        <Card className="card-pad" aria-labelledby="try-t">
          <h2 id="try-t" className="card__title">Try a customer message</h2>
          <div style={{ marginTop: 14 }}>
            <Field label="Message">{(fid) => (
              <textarea id={fid} className="input pg-msg" rows={2} value={text} lang={result.language === 'ta' ? 'ta' : result.language === 'si' ? 'si' : undefined}
                onChange={(e) => setText(e.target.value)} />
            )}</Field>
          </div>
          <div className="pg-presets" role="group" aria-label="Sample messages">
            {(Object.keys(PRESETS) as Preset[]).map((p) => (
              <button key={p} type="button" aria-pressed={preset === p} className={`chip ${preset === p ? 'chip--violet' : 'chip--neutral'}`}
                onClick={() => { setPreset(p); setText(PRESETS[p]); setResult(classify(PRESETS[p])) }}>{p}</button>
            ))}
          </div>
          <Button variant="green" style={{ marginTop: 18 }} onClick={() => setResult(classify(text))}>Classify</Button>
          <p className="note" style={{ marginTop: 18 }}>Demo mode returns scripted classifications. No Gemini call is made.</p>
        </Card>

        <Card className="card-pad" aria-labelledby="cls-t">
          <h2 id="cls-t" className="card__title">1 · AI classification</h2>
          <pre className="code" aria-live="polite">{json}</pre>
        </Card>

        <Card className="card-pad" aria-labelledby="dec-t">
          <h2 id="dec-t" className="card__title">2 · Code decision</h2>
          <dl className="kv" style={{ marginTop: 8 }}>
            <div className="kv__row"><dt>List price</dt><dd>{product ? lkr(product.price) : '—'}</dd></div>
            <div className="kv__row"><dt>Min price</dt><dd>{product?.minPrice !== undefined ? lkr(product.minPrice) : '—'}</dd></div>
            <div className="kv__row"><dt>Offer</dt><dd>{result.offer ? lkr(result.offer) : '—'}</dd></div>
            <div className="kv__row"><dt>Stock</dt><dd>{product ? `${product.stock} available` : '—'}</dd></div>
          </dl>
          {decision ? (
            <p className={`decision decision--${decision.outcome}`} role="status">{decisionLabel(decision)}</p>
          ) : (
            <p className="decision decision--accept" role="status">
              {result.intent === 'order' && product ? (product.stock > 0 ? 'Order at list price: stock available' : 'Out of stock: no order created') : result.intent === 'product_search' ? 'Search: catalogue match returned' : 'No business action: help reply sent'}
            </p>
          )}
        </Card>
      </div>
    </>
  )
}
