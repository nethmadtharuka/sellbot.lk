import { useCallback, useEffect, useState } from 'react'
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { sellbotApi } from '../api/sellbotApi'
import type { AnalyticsSummaryDto } from '../api/types'
import { Badge, Button, Card, Field, Input, Table } from '../components/ui'
import { StatCard } from '../components/StatCard'

const PIE_COLORS = ['#60a5fa', '#34d399', '#fbbf24', '#fb7185', '#a78bfa', '#94a3b8']

function toLocalDate(d: Date): string {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

function toneForStatus(s: string): 'neutral' | 'good' | 'warn' | 'bad' {
  switch (s.toLowerCase()) {
    case 'delivered': return 'good'
    case 'cancelled': case 'fraudpending': return 'bad'
    case 'pending': case 'unpaid': return 'warn'
    case 'paid': return 'good'
    default: return 'neutral'
  }
}

function fmtLkr(n: number) {
  return `LKR ${n.toLocaleString(undefined, { maximumFractionDigits: 0 })}`
}

export function DashboardPage() {
  const now = new Date()
  const [from, setFrom] = useState(toLocalDate(new Date(now.getTime() - 30 * 86_400_000)))
  const [to, setTo] = useState(toLocalDate(now))
  const [data, setData] = useState<AnalyticsSummaryDto | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await sellbotApi.getAnalytics(from, to)
      setData(res.data)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load analytics')
    } finally {
      setLoading(false)
    }
  }, [from, to])

  useEffect(() => { void load() }, [load])

  return (
    <div style={{ display: 'grid', gap: 16 }}>
      <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 12 }}>
        <div>
          <h1 className="h1">Dashboard</h1>
          <div className="muted">Business analytics overview.</div>
        </div>
      </div>

      {/* Date range picker */}
      <Card
        title="Date Range"
        right={
          <Button variant="secondary" onClick={() => void load()} disabled={loading}>
            {loading ? 'Loading...' : 'Refresh'}
          </Button>
        }
      >
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
          <Field label="From">
            <Input type="date" value={from} onChange={e => setFrom(e.target.value)} />
          </Field>
          <Field label="To">
            <Input type="date" value={to} onChange={e => setTo(e.target.value)} />
          </Field>
        </div>
      </Card>

      {error && (
        <Card title="Error"><div className="muted">{error}</div></Card>
      )}

      {data && (
        <>
          {/* KPI stat cards */}
          <div className="statGrid">
            <StatCard label="Total Orders" value={String(data.totalOrders)} tone="brand" />
            <StatCard label="Revenue" value={fmtLkr(data.totalRevenue)} tone="good" />
            <StatCard label="Paid Orders" value={String(data.paidOrders)} tone="good" />
            <StatCard label="Unpaid Orders" value={String(data.unpaidOrders)} tone="warn" />
          </div>

          {/* Revenue over time */}
          <Card title="Revenue Over Time">
            <div style={{ width: '100%', height: 300 }}>
              <ResponsiveContainer>
                <AreaChart data={data.revenueByDay} margin={{ top: 10, right: 10, left: 0, bottom: 0 }}>
                  <defs>
                    <linearGradient id="gradRevenue" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="#60a5fa" stopOpacity={0.35} />
                      <stop offset="95%" stopColor="#60a5fa" stopOpacity={0} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.08)" />
                  <XAxis
                    dataKey="date"
                    tick={{ fill: 'rgba(255,255,255,0.6)', fontSize: 11 }}
                    tickFormatter={v => v.slice(5)}
                  />
                  <YAxis
                    tick={{ fill: 'rgba(255,255,255,0.6)', fontSize: 11 }}
                    tickFormatter={v => `${(Number(v) / 1000).toFixed(0)}k`}
                  />
                  <Tooltip
                    contentStyle={{ background: '#1e293b', border: '1px solid rgba(255,255,255,0.12)', borderRadius: 10, color: '#fff' }}
                    formatter={(value) => [fmtLkr(Number(value)), 'Revenue']}
                    labelFormatter={l => `Date: ${l}`}
                  />
                  <Area type="monotone" dataKey="revenue" stroke="#60a5fa" strokeWidth={2} fill="url(#gradRevenue)" />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          </Card>

          {/* Two charts side by side */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            {/* Top products bar chart */}
            <Card title="Top Products">
              <div style={{ width: '100%', height: 280 }}>
                <ResponsiveContainer>
                  <BarChart data={data.topProducts} layout="vertical" margin={{ top: 5, right: 20, left: 10, bottom: 5 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.08)" />
                    <XAxis
                      type="number"
                      tick={{ fill: 'rgba(255,255,255,0.6)', fontSize: 11 }}
                      tickFormatter={v => `${(Number(v) / 1000).toFixed(0)}k`}
                    />
                    <YAxis
                      type="category"
                      dataKey="productName"
                      width={140}
                      tick={{ fill: 'rgba(255,255,255,0.6)', fontSize: 11 }}
                      tickFormatter={v => v.length > 20 ? v.slice(0, 18) + '...' : v}
                    />
                    <Tooltip
                      contentStyle={{ background: '#1e293b', border: '1px solid rgba(255,255,255,0.12)', borderRadius: 10, color: '#fff' }}
                      formatter={(value) => [fmtLkr(Number(value)), 'Revenue']}
                    />
                    <Bar dataKey="revenue" radius={[0, 6, 6, 0]}>
                      {data.topProducts.map((_, i) => (
                        <Cell key={i} fill={PIE_COLORS[i % PIE_COLORS.length]} />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </Card>

            {/* Orders by status pie chart */}
            <Card title="Orders by Status">
              <div style={{ width: '100%', height: 280 }}>
                <ResponsiveContainer>
                  <PieChart>
                    <Pie
                      data={data.ordersByStatus}
                      dataKey="count"
                      nameKey="status"
                      cx="50%"
                      cy="50%"
                      innerRadius={55}
                      outerRadius={90}
                      paddingAngle={3}
                      label={({ name, value }) => `${name} (${value})`}
                    >
                      {data.ordersByStatus.map((_, i) => (
                        <Cell key={i} fill={PIE_COLORS[i % PIE_COLORS.length]} />
                      ))}
                    </Pie>
                    <Tooltip
                      contentStyle={{ background: '#1e293b', border: '1px solid rgba(255,255,255,0.12)', borderRadius: 10, color: '#fff' }}
                    />
                    <Legend
                      wrapperStyle={{ color: 'rgba(255,255,255,0.7)', fontSize: 12 }}
                    />
                  </PieChart>
                </ResponsiveContainer>
              </div>
            </Card>
          </div>

          {/* Recent orders table */}
          <Card title="Recent Orders">
            <Table>
              <table>
                <thead>
                  <tr>
                    <th>Order #</th>
                    <th>Customer</th>
                    <th>Status</th>
                    <th>Payment</th>
                    <th>Total</th>
                    <th>Date</th>
                  </tr>
                </thead>
                <tbody>
                  {data.recentOrders.length === 0 && (
                    <tr><td colSpan={6} className="muted">No orders in this period.</td></tr>
                  )}
                  {data.recentOrders.map(o => (
                    <tr key={o.orderNumber}>
                      <td style={{ fontWeight: 700 }}>{o.orderNumber}</td>
                      <td>{o.customerName || '\u2014'}</td>
                      <td><Badge tone={toneForStatus(o.status)}>{o.status}</Badge></td>
                      <td><Badge tone={toneForStatus(o.paymentStatus)}>{o.paymentStatus}</Badge></td>
                      <td>{o.totalAmount.toLocaleString(undefined, { maximumFractionDigits: 0 })}</td>
                      <td className="muted" style={{ fontSize: 12 }}>{new Date(o.createdAt).toLocaleString()}</td>
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
