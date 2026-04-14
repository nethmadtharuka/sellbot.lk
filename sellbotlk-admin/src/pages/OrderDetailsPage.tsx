import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { sellbotApi } from '../api/sellbotApi'
import type { OrderResponseDto } from '../api/types'
import { Badge, Button, Card, Field, Input, Select, Table } from '../components/ui'

type LoadState =
  | { kind: 'loading' }
  | { kind: 'loaded'; order: OrderResponseDto }
  | { kind: 'error'; message: string }

const orderStatuses = ['Pending', 'Confirmed', 'Processing', 'Dispatched', 'Delivered', 'Cancelled', 'FraudPending']

function toneForOrderStatus(os: string): 'neutral' | 'good' | 'warn' | 'bad' {
  switch (os.toLowerCase()) {
    case 'delivered':
      return 'good'
    case 'cancelled':
      return 'bad'
    case 'fraudpending':
      return 'bad'
    case 'pending':
      return 'warn'
    default:
      return 'neutral'
  }
}

function toneForPaymentStatus(ps: string): 'neutral' | 'good' | 'warn' | 'bad' {
  switch (ps.toLowerCase()) {
    case 'paid':
      return 'good'
    case 'unpaid':
      return 'warn'
    case 'refunded':
      return 'neutral'
    default:
      return 'neutral'
  }
}

export function OrderDetailsPage() {
  const { id } = useParams()
  const orderId = useMemo(() => Number(id), [id])

  const [state, setState] = useState<LoadState>({ kind: 'loading' })
  const [newStatus, setNewStatus] = useState<string>('Processing')
  const [deliveryStatus, setDeliveryStatus] = useState<string>('Dispatched')
  const [driverNote, setDriverNote] = useState<string>('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    setState({ kind: 'loading' })
    try {
      const res = await sellbotApi.getOrderById(orderId)
      setState({ kind: 'loaded', order: res.data })
      setNewStatus(res.data.status)
      setDeliveryStatus(res.data.status)
      setDriverNote('')
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Failed to load order'
      setState({ kind: 'error', message: msg })
    }
  }, [orderId])

  useEffect(() => {
    if (!Number.isFinite(orderId)) {
      setState({ kind: 'error', message: 'Invalid order id' })
      return
    }
    void load()
  }, [load, orderId])

  const order = state.kind === 'loaded' ? state.order : null

  async function run<T>(fn: () => Promise<T>) {
    setBusy(true)
    try {
      await fn()
      await load()
    } finally {
      setBusy(false)
    }
  }

  return (
    <div style={{ display: 'grid', gap: 12 }}>
      <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 12 }}>
        <div>
          <h1 className="h1">Order Details</h1>
          <div className="muted">
            <Link to="/orders" className="navLink">
              ← Back to Orders
            </Link>
          </div>
        </div>
        {order && (
          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <Badge tone={toneForOrderStatus(order.status)}>{order.status}</Badge>
            <Badge tone={toneForPaymentStatus(order.paymentStatus)}>{order.paymentStatus}</Badge>
          </div>
        )}
      </div>

      {state.kind === 'error' && (
        <Card title="Error">
          <div className="muted">{state.message}</div>
        </Card>
      )}

      {state.kind === 'loaded' && order && (
        <>
          <Card
            title={
              <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
                <span style={{ fontWeight: 800 }}>{order.orderNumber}</span>
                {order.isFraudFlagged && <Badge tone="bad">Fraud flagged</Badge>}
              </div>
            }
            right={
              <div style={{ display: 'flex', gap: 8 }}>
                <Button variant="secondary" onClick={() => void load()} disabled={busy}>
                  Refresh
                </Button>
                <Button variant="danger" onClick={() => void run(() => sellbotApi.cancelOrder(order.id))} disabled={busy}>
                  Cancel order
                </Button>
              </div>
            }
          >
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
              <div>
                <div className="muted" style={{ fontSize: 12 }}>
                  Customer
                </div>
                <div style={{ fontWeight: 700 }}>{order.customerName || '—'}</div>
                <div className="muted">{order.customerPhone || '—'}</div>
              </div>
              <div>
                <div className="muted" style={{ fontSize: 12 }}>
                  Total (LKR)
                </div>
                <div style={{ fontWeight: 800, fontSize: 18 }}>
                  {order.totalAmount.toLocaleString(undefined, { maximumFractionDigits: 0 })}
                </div>
                {order.discountAmount > 0 && (
                  <div className="muted">Discount: {order.discountAmount.toLocaleString(undefined, { maximumFractionDigits: 0 })}</div>
                )}
              </div>
              <div>
                <div className="muted" style={{ fontSize: 12 }}>
                  Delivery
                </div>
                <div style={{ fontWeight: 700 }}>{order.deliveryArea || '—'}</div>
                <div className="muted" style={{ fontSize: 12 }}>
                  {order.deliveryAddress || '—'}
                </div>
              </div>
              <div>
                <div className="muted" style={{ fontSize: 12 }}>
                  Timestamps
                </div>
                <div className="muted" style={{ fontSize: 12 }}>
                  Created: {new Date(order.createdAt).toLocaleString()}
                </div>
                <div className="muted" style={{ fontSize: 12 }}>
                  Updated: {new Date(order.updatedAt).toLocaleString()}
                </div>
              </div>
            </div>
            {order.fraudReason && (
              <div style={{ marginTop: 10 }} className="muted">
                Fraud reason: {order.fraudReason}
              </div>
            )}
          </Card>

          <Card title="Actions">
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
              <div style={{ display: 'grid', gap: 10 }}>
                <Field label="Update order status" hint="Calls PUT /api/v1/orders/{id}/status">
                  <Select value={newStatus} onChange={(e) => setNewStatus(e.target.value)}>
                    {orderStatuses.map((s) => (
                      <option key={s} value={s}>
                        {s}
                      </option>
                    ))}
                  </Select>
                </Field>
                <Button
                  onClick={() => void run(() => sellbotApi.updateOrderStatus(order.id, { status: newStatus }))}
                  disabled={busy || !newStatus.trim()}
                >
                  Save status
                </Button>
              </div>

              <div style={{ display: 'grid', gap: 10 }}>
                <Field label="Update delivery status" hint="Calls PUT /api/v1/orders/{id}/delivery-status">
                  <Select value={deliveryStatus} onChange={(e) => setDeliveryStatus(e.target.value)}>
                    {['Confirmed', 'Processing', 'Dispatched', 'Delivered'].map((s) => (
                      <option key={s} value={s}>
                        {s}
                      </option>
                    ))}
                  </Select>
                </Field>
                <Field label="Driver note (optional)">
                  <Input value={driverNote} onChange={(e) => setDriverNote(e.target.value)} placeholder="e.g. Call customer before arriving" />
                </Field>
                <Button
                  onClick={() =>
                    void run(() => sellbotApi.updateDeliveryStatus(order.id, { status: deliveryStatus, driverNote }))
                  }
                  disabled={busy || !deliveryStatus.trim()}
                >
                  Save delivery status
                </Button>
              </div>
            </div>
          </Card>

          <Card title={`Items (${order.items.length})`}>
            <Table>
              <table>
                <thead>
                  <tr>
                    <th>Product</th>
                    <th>Qty</th>
                    <th>Unit</th>
                    <th>Negotiated</th>
                    <th>Effective</th>
                    <th>Line total</th>
                  </tr>
                </thead>
                <tbody>
                  {order.items.map((it) => (
                    <tr key={it.id}>
                      <td style={{ fontWeight: 700 }}>{it.productName}</td>
                      <td>{it.quantity}</td>
                      <td>{it.unitPrice.toLocaleString(undefined, { maximumFractionDigits: 0 })}</td>
                      <td>{it.negotiatedPrice == null ? '—' : it.negotiatedPrice.toLocaleString(undefined, { maximumFractionDigits: 0 })}</td>
                      <td>{it.effectiveUnitPrice.toLocaleString(undefined, { maximumFractionDigits: 0 })}</td>
                      <td style={{ fontWeight: 700 }}>{it.lineTotal.toLocaleString(undefined, { maximumFractionDigits: 0 })}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </Table>
          </Card>
        </>
      )}
    </div>
  )
}

