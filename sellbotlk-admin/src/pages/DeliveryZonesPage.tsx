import { useCallback, useEffect, useState } from 'react'
import { sellbotApi } from '../api/sellbotApi'
import type { DeliveryCheckResponseDto, DeliveryZoneResponseDto } from '../api/types'
import { Badge, Button, Card, Field, Input, Table } from '../components/ui'

type LoadState =
  | { kind: 'loading' }
  | { kind: 'loaded'; zones: DeliveryZoneResponseDto[] }
  | { kind: 'error'; message: string }

function toneForActive(active: boolean): 'neutral' | 'good' | 'bad' {
  return active ? 'good' : 'bad'
}

export function DeliveryZonesPage() {
  const [state, setState] = useState<LoadState>({ kind: 'loading' })
  const [area, setArea] = useState('')
  const [orderTotal, setOrderTotal] = useState('0')
  const [checkResult, setCheckResult] = useState<DeliveryCheckResponseDto | null>(null)
  const [checking, setChecking] = useState(false)

  const load = useCallback(async () => {
    setState({ kind: 'loading' })
    try {
      const res = await sellbotApi.getDeliveryZones()
      setState({ kind: 'loaded', zones: res.data })
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Failed to load delivery zones'
      setState({ kind: 'error', message: msg })
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  async function check() {
    const total = Number(orderTotal)
    if (!area.trim() || !Number.isFinite(total)) return
    setChecking(true)
    setCheckResult(null)
    try {
      const res = await sellbotApi.checkDelivery({ area: area.trim(), orderTotal: total })
      setCheckResult(res.data)
    } catch (e) {
      setCheckResult({
        isServiceable: false,
        zoneName: '',
        deliveryFee: 0,
        estimatedDays: 0,
        isFreeDelivery: false,
        message: e instanceof Error ? e.message : 'Delivery check failed',
      })
    } finally {
      setChecking(false)
    }
  }

  const zones = state.kind === 'loaded' ? state.zones : []

  return (
    <div style={{ display: 'grid', gap: 12 }}>
      <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 12 }}>
        <div>
          <h1 className="h1">Delivery Zones</h1>
          <div className="muted">Zones + quick “can we deliver?” checker.</div>
        </div>
        <Button variant="secondary" onClick={() => void load()} disabled={state.kind === 'loading'}>
          Refresh
        </Button>
      </div>

      <Card title="Check delivery fee">
        <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr auto', gap: 10, alignItems: 'end' }}>
          <Field label="Area">
            <Input value={area} onChange={(e) => setArea(e.target.value)} placeholder="e.g. Colombo 03" />
          </Field>
          <Field label="Order total (LKR)">
            <Input value={orderTotal} onChange={(e) => setOrderTotal(e.target.value)} inputMode="decimal" />
          </Field>
          <Button onClick={() => void check()} disabled={checking || !area.trim() || Number.isNaN(Number(orderTotal))}>
            Check
          </Button>
        </div>

        {checkResult && (
          <div style={{ marginTop: 12, display: 'grid', gap: 8 }}>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <Badge tone={checkResult.isServiceable ? 'good' : 'bad'}>
                {checkResult.isServiceable ? 'Serviceable' : 'Not serviceable'}
              </Badge>
              {checkResult.isServiceable && checkResult.isFreeDelivery && <Badge tone="good">Free delivery</Badge>}
            </div>
            <div className="muted">{checkResult.message}</div>
            {checkResult.isServiceable && (
              <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
                <div>
                  <div className="muted" style={{ fontSize: 12 }}>
                    Zone
                  </div>
                  <div style={{ fontWeight: 700 }}>{checkResult.zoneName}</div>
                </div>
                <div>
                  <div className="muted" style={{ fontSize: 12 }}>
                    Fee (LKR)
                  </div>
                  <div style={{ fontWeight: 700 }}>
                    {checkResult.deliveryFee.toLocaleString(undefined, { maximumFractionDigits: 0 })}
                  </div>
                </div>
                <div>
                  <div className="muted" style={{ fontSize: 12 }}>
                    ETA (days)
                  </div>
                  <div style={{ fontWeight: 700 }}>{checkResult.estimatedDays}</div>
                </div>
              </div>
            )}
          </div>
        )}
      </Card>

      {state.kind === 'error' && (
        <Card title="Error">
          <div className="muted">{state.message}</div>
        </Card>
      )}

      <Card title={`Zones (${zones.length})`}>
        <Table>
          <table>
            <thead>
              <tr>
                <th>ID</th>
                <th>Zone</th>
                <th>Fee (LKR)</th>
                <th>ETA (days)</th>
                <th>Free threshold</th>
                <th>Active</th>
              </tr>
            </thead>
            <tbody>
              {state.kind === 'loading' && (
                <tr>
                  <td colSpan={6} className="muted">
                    Loading…
                  </td>
                </tr>
              )}
              {state.kind === 'loaded' && zones.length === 0 && (
                <tr>
                  <td colSpan={6} className="muted">
                    No zones found.
                  </td>
                </tr>
              )}
              {state.kind === 'loaded' &&
                zones.map((z) => (
                  <tr key={z.id}>
                    <td>{z.id}</td>
                    <td style={{ fontWeight: 700 }}>{z.zoneName}</td>
                    <td>{z.deliveryFee.toLocaleString(undefined, { maximumFractionDigits: 0 })}</td>
                    <td>{z.estimatedDays}</td>
                    <td className="muted" style={{ fontSize: 12 }}>
                      {z.freeDeliveryThreshold == null
                        ? '—'
                        : z.freeDeliveryThreshold.toLocaleString(undefined, { maximumFractionDigits: 0 })}
                    </td>
                    <td>
                      <Badge tone={toneForActive(z.isActive)}>{z.isActive ? 'Active' : 'Inactive'}</Badge>
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
        </Table>
      </Card>
    </div>
  )
}

