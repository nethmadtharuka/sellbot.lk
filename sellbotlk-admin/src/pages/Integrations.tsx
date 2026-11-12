import { Card, Chip, PageHeader } from '../ui/primitives'

/** Static by design: keys live on the server and there are no health checks to show. */
const INTEGRATIONS = [
  { name: 'WhatsApp Cloud API', desc: 'Receives and sends customer messages' },
  { name: 'Gemini', desc: 'Classifies language and intent' },
  { name: 'Database', desc: 'Orders, products, customers' },
  { name: 'File storage', desc: 'Payment slip images' },
]

export default function Integrations() {
  return (
    <>
      <PageHeader eyebrow="System" title="Integrations" />
      <div className="int-grid">
        {INTEGRATIONS.map((i) => (
          <Card key={i.name} className="int" aria-label={i.name}>
            <div className="row-between" style={{ alignItems: 'flex-start' }}>
              <h2 className="int__name">{i.name}</h2>
              <Chip tone="tag">Set by environment</Chip>
            </div>
            <p className="int__desc">{i.desc}</p>
            <p className="int__secret mono" aria-label="Value hidden">••••••••••••••••</p>
          </Card>
        ))}
      </div>
      <p className="note" style={{ marginTop: 26 }}>Keys are configured on the server and never shown here. Live health checks are not part of this system, so no health status is displayed.</p>
    </>
  )
}
