import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { sellbotApi } from '../api/sellbotApi'
import type { OrderResponseDto } from '../api/types'
import { Badge, Button, Card, Field, Input, Select, Table } from '../components/ui'

type LoadState =
  | { kind: 'idle' | 'loading' }
  | { kind: 'loaded'; orders: OrderResponseDto[] }
  | { kind: 'error'; message: string }

const statusOptions = [
  '',
  'Pending',
  'Confirmed',
  'Processing',
  'Dispatched',
  'Delivered',
  'Cancelled',
  'FraudPending',
] as const

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

export function OrdersPage() {
  const [status, setStatus] = useState<string>('')
  const [customerId, setCustomerId] = useState<string>('')
  const [state, setState] = useState<LoadState>({ kind: 'idle' })

  const params = useMemo(() => {
    const cid = customerId.trim()
    return {
      status: status.trim() || undefined,
      customerId: cid ? Number(cid) : undefined,
    }
  }, [customerId, status])

  const load = useCallback(async () => {
    setState({ kind: 'loading' })
    try {
      const res = await sellbotApi.getOrders(params)
      setState({ kind: 'loaded', orders: res.data })
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Failed to load orders'
      setState({ kind: 'error', message: msg })
    }
  }, [params])

  useEffect(() => {
    void load()
  }, [load])

  const orders = state.kind === 'loaded' ? state.orders : []

  return (
    <div style={{ display: 'grid', gap: 12 }}>
      <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 12 }}>
        <div>
          <h1 className="h1">Orders</h1>
          <div className="muted">View orders and drill into details.</div>
        </div>
        <div className="muted" style={{ fontSize: 12 }}>
          Tip: set `VITE_API_BASE_URL` if your API runs on a different port.
        </div>
      </div>

      <Card
        title="Filters"
        right={
          <div style={{ display: 'flex', gap: 8 }}>
            <Button variant="secondary" onClick={() => void load()} disabled={state.kind === 'loading'}>
              Refresh
            </Button>
          </div>
        }
      >
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr auto', gap: 10, alignItems: 'end' }}>
          <Field label="Status">
            <Select value={status} onChange={(e) => setStatus(e.target.value)}>
              {statusOptions.map((s) => (
                <option key={s} value={s}>
                  {s || 'All'}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Customer ID (optional)">
            <Input
              value={customerId}
              onChange={(e) => setCustomerId(e.target.value)}
              inputMode="numeric"
              placeholder="e.g. 12"
            />
          </Field>
          <Button
            onClick={() => void load()}
            disabled={state.kind === 'loading' || (customerId.trim() !== '' && Number.isNaN(Number(customerId)))}
          >
            Apply
          </Button>
        </div>
      </Card>

      {state.kind === 'error' && (
        <Card title="Error">
          <div className="muted">{state.message}</div>
        </Card>
      )}

      <Card title={`Orders (${orders.length})`}>
        <Table>
          <table>
            <thead>
              <tr>
                <th>ID</th>
                <th>Order #</th>
                <th>Customer</th>
                <th>Status</th>
                <th>Payment</th>
                <th>Total</th>
                <th>Updated</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {state.kind === 'loading' && (
                <tr>
                  <td colSpan={8} className="muted">
                    Loading…
                  </td>
                </tr>
              )}
              {state.kind === 'loaded' && orders.length === 0 && (
                <tr>
                  <td colSpan={8} className="muted">
                    No orders found.
                  </td>
                </tr>
              )}
              {state.kind === 'loaded' &&
                orders.map((o) => (
                  <tr key={o.id}>
                    <td>{o.id}</td>
                    <td style={{ fontWeight: 700 }}>{o.orderNumber}</td>
                    <td>
                      <div style={{ display: 'grid' }}>
                        <span>{o.customerName || '—'}</span>
                        <span className="muted" style={{ fontSize: 12 }}>
                          {o.customerPhone || '—'}
                        </span>
                      </div>
                    </td>
                    <td>
                      <Badge tone={toneForOrderStatus(o.status)}>{o.status}</Badge>
                    </td>
                    <td>
                      <Badge tone={toneForPaymentStatus(o.paymentStatus)}>{o.paymentStatus}</Badge>
                    </td>
                    <td>{o.totalAmount.toLocaleString(undefined, { maximumFractionDigits: 0 })}</td>
                    <td className="muted" style={{ fontSize: 12 }}>
                      {new Date(o.updatedAt).toLocaleString()}
                    </td>
                    <td>
                      <Link to={`/orders/${o.id}`} className="navLink">
                        View
                      </Link>
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

