import { nextFulfilmentStatus, type OrderStatus } from '../lib/status'
import {
  demoAnalytics, demoConversations, demoCustomers, demoDocuments, demoOrders, demoOverview, demoProducts, listedCustomerIds,
} from './demo'
import type { DataSource } from './source'
import type { Order, Product } from './types'

// In-memory copies: mutations live until reload ("changes reset on reload").
let orders: Order[] = structuredClone(demoOrders)
let products: Product[] = structuredClone(demoProducts)

const sim = <T,>(data: T) => Promise.resolve({ data, simulated: true })

function patchOrder(id: number, patch: Partial<Order>) {
  orders = orders.map((o) => (o.id === id ? { ...o, ...patch, updatedAt: new Date().toISOString() } : o))
}

export const demoSource: DataSource = {
  mode: 'demo',
  overview: () => sim(demoOverview),
  orders: () => sim(orders),
  order: (id) => sim(orders.find((o) => o.id === id) ?? null),
  advanceOrder: async (id: number, next: OrderStatus) => {
    const o = orders.find((x) => x.id === id)
    if (!o || nextFulfilmentStatus(o.status) !== next) throw new Error(`Cannot move ${o?.status ?? 'order'} to ${next}.`)
    patchOrder(id, { status: next })
  },
  cancelOrder: async (id) => patchOrder(id, { status: 'Cancelled' }),
  approvePayment: async (id) => {
    const o = orders.find((x) => x.id === id)
    if (o?.payment !== 'Pending review') throw new Error('This order has no slip awaiting review.')
    patchOrder(id, { payment: 'Paid' })
  },
  rejectPayment: async (id) => {
    const o = orders.find((x) => x.id === id)
    if (o?.payment !== 'Pending review') throw new Error('This order has no slip awaiting review.')
    patchOrder(id, { payment: 'Unpaid', slipReceived: undefined })
  },
  slipImage: async () => null,
  products: () => sim(products),
  saveProduct: async (p) => {
    products = products.some((x) => x.id === p.id) ? products.map((x) => (x.id === p.id ? p : x)) : [...products, p]
  },
  customers: () => sim(demoCustomers.filter((c) => listedCustomerIds.includes(c.id))),
  customer: (id) => {
    const customer = demoCustomers.find((c) => c.id === id)
    return sim(customer ? { customer, orders: orders.filter((o) => o.customerId === id) } : null)
  },
  conversations: () => sim(demoConversations),
  analytics: () => sim(demoAnalytics),
  documents: () => sim(demoDocuments),
}
