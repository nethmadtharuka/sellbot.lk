/**
 * LIVE layer: the existing ASP.NET API, unchanged. Only endpoints that exist are called.
 * Screens with no backend endpoint fall back to the mock layer and are flagged simulated.
 */
import { apiGet, apiGetBlob, apiPut } from '../api/http'
import type { AnalyticsSummaryDto, ApiEnvelope, OrderResponseDto, ProductResponseDto } from '../api/types'
import { dayMonth } from '../lib/format'
import { mapOrderStatus, mapPaymentStatus, type OrderStatus } from '../lib/status'
import { demoSource } from './demoSource'
import type { DataSource } from './source'
import type { Order, Product } from './types'

const real = <T,>(data: T) => ({ data, simulated: false })

function toOrder(o: OrderResponseDto): Order | null {
  const status = mapOrderStatus(o.status)
  const payment = mapPaymentStatus(o.paymentStatus)
  if (!status || !payment) return null // FraudPending / Pending / Refunded are never set by the backend
  return {
    id: o.id, number: o.orderNumber, customerName: o.customerName, customerPhone: o.customerPhone,
    createdAt: o.createdAt, updatedAt: o.updatedAt, total: o.totalAmount, status, payment,
    slipReceived: payment === 'Pending review' ? o.updatedAt : undefined,
    items: o.items.map((i) => ({ name: i.productName, qty: i.quantity, unitPrice: i.unitPrice, negotiatedPrice: i.negotiatedPrice ?? undefined })),
  }
}

function toProduct(p: ProductResponseDto): Product {
  // The API exposes isLowStock but not LowStockThreshold or MinPrice.
  return {
    id: p.id, name: p.name, category: p.category, description: p.description ?? undefined, price: p.price,
    stock: p.stockQty, lowStockThreshold: p.isLowStock ? p.stockQty : -1, active: p.isActive,
  }
}

async function allOrders(): Promise<Order[]> {
  const res = await apiGet<ApiEnvelope<OrderResponseDto[]>>('/api/v1/orders')
  return res.data.map(toOrder).filter((o): o is Order => o !== null)
}

function range30() {
  const to = new Date(), from = new Date(to.getTime() - 30 * 86_400_000)
  return `from=${from.toISOString()}&to=${to.toISOString()}`
}

export const liveSource: DataSource = {
  mode: 'live',
  async overview() {
    const [summary, orders, low] = await Promise.all([
      apiGet<ApiEnvelope<AnalyticsSummaryDto>>(`/api/v1/analytics/summary?${range30()}`),
      allOrders(),
      apiGet<ApiEnvelope<ProductResponseDto[]>>('/api/v1/products/low-stock'),
    ])
    const s = summary.data
    const review = orders.filter((o) => o.payment === 'Pending review').sort((a, b) => a.updatedAt.localeCompare(b.updatedAt))
    const unpaid = orders.filter((o) => o.payment === 'Unpaid' && o.status === 'Confirmed')
    const count = (st: string) => s.ordersByStatus.find((x) => x.status === st)?.count ?? 0
    const hoursOld = review[0] ? Math.max(1, Math.round((Date.now() - new Date(review[0].updatedAt).getTime()) / 3_600_000)) : 0
    const last14 = s.revenueByDay.slice(-14)
    return real({
      revenue30: s.totalRevenue, revenueOrders: s.totalOrders, orders30: s.totalOrders,
      ordersBreakdown: `${count('Confirmed')} Confirmed · ${count('Dispatched')} Dispatched`,
      paymentsToReview: review.length, oldestWaiting: review.length ? `Oldest waiting ${hoursOld} h` : 'None waiting',
      lowStock: low.data.length,
      queues: [
        { count: review.length, tone: 'blue', title: 'Payment slips awaiting review', detail: review[0] ? `Oldest: ${review[0].number} · LKR ${review[0].total.toLocaleString('en-US')}` : 'Nothing waiting', action: 'Review', to: '/app/payments' },
        { count: unpaid.length, tone: 'amber', title: 'Unpaid orders expiring soon', detail: 'Auto-cancelled after 24 h, stock returns', action: 'Open', to: '/app/orders?payment=Unpaid' },
        { count: low.data.length, tone: 'red', title: 'Products low on stock', detail: low.data[0] ? `${low.data[0].name} · ${low.data[0].stockQty} left` : 'All above threshold', action: 'Restock', to: '/app/products' },
      ],
      revenue14: last14.map((d, i) => ({ label: i === 0 || i === 6 || i === last14.length - 1 ? dayMonth(d.date) : '', value: d.revenue })),
    })
  },
  orders: async () => real(await allOrders()),
  async order(id) {
    const res = await apiGet<ApiEnvelope<OrderResponseDto>>(`/api/v1/orders/${id}`)
    return real(toOrder(res.data))
  },
  async advanceOrder(id: number, next: OrderStatus) {
    // Validated path: DeliveryService.IsValidTransition + WhatsApp notification to the customer.
    await apiPut(`/api/v1/orders/${id}/delivery-status`, { status: next })
  },
  async cancelOrder(id) { await apiPut(`/api/v1/orders/${id}/cancel`, {}) },
  async approvePayment(id) { await apiPut(`/api/v1/orders/${id}/payment/approve`, {}) },
  async rejectPayment(id, reason) { await apiPut(`/api/v1/orders/${id}/payment/reject`, { reason }) },
  async slipImage(id) {
    try { return URL.createObjectURL(await apiGetBlob(`/api/v1/orders/${id}/payment-slip`)) } catch { return null }
  },
  async products() {
    const res = await apiGet<ApiEnvelope<ProductResponseDto[]>>('/api/v1/products')
    return real(res.data.map(toProduct))
  },
  async saveProduct(p) {
    await apiPut(`/api/v1/products/${p.id}`, {
      name: p.name, category: p.category, description: p.description, price: p.price,
      minPrice: p.minPrice, stockQty: p.stock, isActive: p.active,
    })
  },
  // No backend endpoints: mock layer, flagged simulated.
  customers: demoSource.customers,
  customer: demoSource.customer,
  conversations: demoSource.conversations,
  documents: demoSource.documents,
  async analytics() {
    const s = (await apiGet<ApiEnvelope<AnalyticsSummaryDto>>(`/api/v1/analytics/summary?${range30()}`)).data
    const mock = (await demoSource.analytics()).data
    const byStatus = (['Confirmed', 'Processing', 'Dispatched', 'Delivered', 'Cancelled'] as const)
      .map((st) => ({ status: st, count: s.ordersByStatus.find((x) => x.status === st)?.count ?? 0 }))
    return real({
      ordersPerDay: s.revenueByDay.slice(-14).map((d) => d.orders),
      byStatus, totalOrders: s.totalOrders,
      topProducts: s.topProducts.slice(0, 3).map((t) => ({ name: t.productName, sold: t.unitsSold })),
      languages: mock.languages, languagesSimulated: true,
    })
  },
}
