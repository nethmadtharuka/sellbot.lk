/**
 * MOCK LAYER — fictional data copied from the approved designs.
 * Used for everything in demo mode, and in live mode for the screens the backend has no
 * endpoint for (customers, inbox, playground, journey, documents list, language mix).
 * Nothing here is real customer data. Changes are held in memory and reset on reload.
 */
import type { OrderStatus, PaymentStatus } from '../lib/status'
import type { Analytics, Conversation, Customer, DocumentRow, Lang, Order, Overview, Product } from './types'

/** The designs are dated 10 Nov 2026, mid-morning. Demo time is pinned so copy like "Expires in 21 h" matches. */
export const DEMO_NOW = new Date('2026-11-10T10:30:00')

const iso = (d: string) => new Date(d).toISOString()

export const demoProducts: Product[] = [
  { id: 1, name: 'Executive High-Back Leather Chair', category: 'Chairs', price: 28500, minPrice: 25000, stock: 19, lowStockThreshold: 5, active: true, description: 'Black leather, tilt and height adjust.' },
  { id: 2, name: 'Three-Seater Fabric Sofa', category: 'Sofas', price: 55000, minPrice: 45000, stock: 6, lowStockThreshold: 3, active: true, description: 'Grey fabric, solid frame.' },
  { id: 3, name: 'Wooden Bookshelf (5-tier)', shortName: 'Bookshelf', category: 'Storage', price: 17575, minPrice: 15000, stock: 3, lowStockThreshold: 5, active: true, description: 'Five shelves, solid wood, 180 cm.' },
  { id: 4, name: 'Dining Table Set', category: 'Dining', price: 18500, minPrice: 16000, stock: 0, lowStockThreshold: 2, active: true, description: 'Table with four chairs.' },
  { id: 5, name: 'Study Desk', category: 'Desks', price: 12200, minPrice: 10500, stock: 14, lowStockThreshold: 4, active: true, description: 'Compact desk with drawer.' },
]

export const demoCustomers: Customer[] = [
  { id: 1, name: 'Kasun D.', phone: '94700000001', lang: 'en', orders: 3, lifetime: 71200, lastOrder: iso('2026-11-10T07:30'), since: iso('2026-10-02') },
  { id: 2, name: 'Ayesha F.', phone: '94700000003', lang: 'en', orders: 2, lifetime: 100000, lastOrder: iso('2026-11-10T08:50'), since: iso('2026-10-14') },
  { id: 3, name: 'Nimali P.', phone: '94700000004', lang: 'si', orders: 1, lifetime: 55000, lastOrder: iso('2026-11-09T15:10'), since: iso('2026-11-01') },
  { id: 4, name: 'Priya S.', phone: '94700000002', lang: 'ta', orders: 2, lifetime: 31000, lastOrder: iso('2026-11-08T11:00'), since: iso('2026-10-20') },
  { id: 5, name: 'Thilini Wijesundara Perera', phone: '94700000007', lang: 'si', orders: 1, lifetime: 0, lastOrder: iso('2026-11-05T09:00'), since: iso('2026-11-05') },
  { id: 6, name: 'Dilshan W.', phone: '94700000006', lang: 'en', orders: 2, lifetime: 0, lastOrder: iso('2026-11-09T13:30'), since: iso('2026-10-28') },
  { id: 7, name: 'Ruwan T.', phone: '94700000005', lang: 'en', orders: 1, lifetime: 12200, lastOrder: iso('2026-11-06T10:00'), since: iso('2026-11-06') },
]
/** The Customers page lists exactly the five people drawn in the design. */
export const listedCustomerIds = [1, 2, 3, 4, 5]

type Seed = [number, string, number, string, 'WhatsApp' | 'Admin', [string, number, number, number?][], number, OrderStatus, PaymentStatus, string?, string?, Lang?]
// [orderNo, customer, customerId, created, channel, items[name, qty, unit, negotiated?], total, status, payment, updatedAt?, slipReceived?]
const drawn: Seed[] = [
  [42, 'Ayesha F.', 2, '2026-11-10T08:50', 'WhatsApp', [['Three-Seater Fabric Sofa', 1, 55000, 45000]], 45000, 'Confirmed', 'Pending review', '2026-11-10T10:04', '2026-11-10T10:04', 'en'],
  [41, 'Kasun D.', 1, '2026-11-10T07:30', 'WhatsApp', [['Executive High-Back Leather Chair', 1, 28500]], 28500, 'Confirmed', 'Unpaid', '2026-11-10T07:30', undefined, 'si'],
  [40, 'Nimali P.', 3, '2026-11-09T15:10', 'WhatsApp', [['Three-Seater Fabric Sofa', 1, 55000]], 55000, 'Processing', 'Paid', undefined, undefined, 'si'],
  [39, 'Dilshan W.', 6, '2026-11-09T13:30', 'Admin', [['Wooden Bookshelf (5-tier)', 2, 17575]], 35150, 'Confirmed', 'Unpaid', '2026-11-09T13:30'],
  [38, 'Dilshan W.', 6, '2026-11-02T12:00', 'WhatsApp', [['Wooden Bookshelf (5-tier)', 2, 17575]], 35150, 'Confirmed', 'Pending review', '2026-11-09T18:40', '2026-11-09T18:40'],
  [37, 'Priya S.', 4, '2026-11-08T11:00', 'WhatsApp', [['Dining Table Set', 1, 18500]], 18500, 'Dispatched', 'Paid', undefined, undefined, 'ta'],
  [36, 'Nimali P.', 3, '2026-11-02T09:00', 'WhatsApp', [['Three-Seater Fabric Sofa', 1, 55000]], 55000, 'Confirmed', 'Pending review', '2026-11-09T16:20', '2026-11-09T16:20'],
  [35, 'Ruwan T.', 7, '2026-11-06T10:00', 'WhatsApp', [['Study Desk', 1, 12200]], 12200, 'Delivered', 'Paid', undefined, undefined, 'en'],
  [33, 'Thilini Wijesundara Perera', 5, '2026-11-05T09:00', 'WhatsApp', [['Executive High-Back Leather Chair', 2, 28500], ['Wooden Bookshelf (5-tier)', 1, 17575]], 75500, 'Cancelled', 'Unpaid'],
  [29, 'Kasun D.', 1, '2026-10-24T10:00', 'WhatsApp', [['Study Desk', 2, 12100]], 24200, 'Delivered', 'Paid'],
  [18, 'Kasun D.', 1, '2026-10-05T10:00', 'WhatsApp', [['Dining Table Set', 1, 18500]], 18500, 'Delivered', 'Paid'],
]
const phoneOf = (customerId: number) => demoCustomers.find((c) => c.id === customerId)?.phone ?? '94700000099'
const langOf = (customerId: number) => demoCustomers.find((c) => c.id === customerId)?.lang

function fromSeed([n, name, cid, created, channel, items, total, status, payment, updated, slip, lang]: Seed): Order {
  const product = (pn: string) => demoProducts.find((p) => p.name === pn)
  return {
    id: n, number: `ORD-2026-${String(n).padStart(3, '0')}`, customerId: cid, customerName: name,
    customerPhone: phoneOf(cid), lang: lang ?? langOf(cid), createdAt: iso(created), updatedAt: iso(updated ?? created),
    channel, total, status, payment, slipReceived: slip ? iso(slip) : undefined,
    items: items.map(([pn, qty, unit, neg]) => ({ name: pn, qty, unitPrice: unit, negotiatedPrice: neg, minPrice: product(pn)?.minPrice })),
  }
}

/**
 * Filler orders so the tab counts in the design hold (46 total: Confirmed 12, Processing 8,
 * Dispatched 5, Delivered 17, Cancelled 4). All dated before 4 Nov so the delivery board
 * (last 7 days) shows exactly the cards drawn in the design.
 */
const fillerNames = ['Sanduni K.', 'Mohamed R.', 'Kavindu L.', 'Shalini M.', 'Tharindu B.', 'Fathima N.', 'Chamara J.', 'Nadeesha A.', 'Arjun V.', 'Ishara G.']
const fillerPlan: [OrderStatus, number][] = [['Confirmed', 7], ['Processing', 7], ['Dispatched', 4], ['Delivered', 14], ['Cancelled', 3]]
function fillers(): Order[] {
  // Numbers stay below ORD-2026-042 (the newest drawn order). The design shows 46 orders but
  // only 42 numbers exist this year, so the 4 oldest fillers carry 2025 numbers.
  const used = new Set(drawn.map((s) => s[0]))
  const free = Array.from({ length: 41 }, (_, i) => i + 1).filter((x) => !used.has(x))
  const out: Order[] = []
  let k = 0
  for (const [status, count] of fillerPlan) {
    for (let i = 0; i < count; i++, k++) {
      const slot = free.length - 1 - k // newest fillers take the highest free numbers
      const n = slot >= 0 ? free[slot] : 900 + k
      const year = slot >= 0 ? 2026 : 2025
      const p = demoProducts[k % demoProducts.length]
      const created = new Date(year, year === 2026 ? 9 : 11, year === 2026 ? 31 - Math.min(k, 30) : 20 - (k % 20), 9 + (k % 8), 15).toISOString()
      const payment: PaymentStatus = status === 'Confirmed' || status === 'Cancelled' ? 'Unpaid' : 'Paid'
      const cid = 100 + (k % fillerNames.length)
      out.push({
        id: n, number: `ORD-${year}-${String(year === 2026 ? n : n - 800).padStart(3, '0')}`, customerId: cid, customerName: fillerNames[k % fillerNames.length],
        customerPhone: `9470000${String(10 + k).padStart(4, '0')}`, createdAt: created, updatedAt: created,
        channel: k % 5 === 0 ? 'Admin' : 'WhatsApp', items: [{ name: p.name, qty: 1, unitPrice: p.price, minPrice: p.minPrice }],
        total: p.price, status, payment,
      })
    }
  }
  return out
}

export const demoOrders: Order[] = [...drawn.map(fromSeed), ...fillers()].sort((a, b) => b.createdAt.localeCompare(a.createdAt))

export const demoOverview: Overview = {
  revenue30: 1284000,
  revenueOrders: 46,
  orders30: 46,
  ordersBreakdown: '6 Confirmed · 3 Dispatched',
  paymentsToReview: 3,
  oldestWaiting: 'Oldest waiting 2 h',
  lowStock: 2,
  queues: [
    { count: 3, tone: 'blue', title: 'Payment slips awaiting review', detail: 'Oldest: ORD-2026-041 · LKR 28,500', action: 'Review', to: '/app/payments' },
    { count: 2, tone: 'amber', title: 'Unpaid orders expiring soon', detail: 'Auto-cancelled after 24 h, stock returns', action: 'Open', to: '/app/orders?payment=Unpaid' },
    { count: 2, tone: 'red', title: 'Products low on stock', detail: 'Wooden Bookshelf (5-tier) · 3 left', action: 'Restock', to: '/app/products' },
  ],
  revenue14: [
    { label: '28 Oct', value: 38 }, { label: '', value: 44 }, { label: '', value: 41 }, { label: '', value: 52 },
    { label: '', value: 48 }, { label: '', value: 56 }, { label: '4 Nov', value: 52 }, { label: '', value: 62 },
    { label: '', value: 58 }, { label: '', value: 67 }, { label: '', value: 64 }, { label: '', value: 73 },
    { label: '', value: 70 }, { label: '10 Nov', value: 79 },
  ],
}

export const demoAnalytics: Analytics = {
  ordersPerDay: [7, 9, 8, 11.6, 10.2, 13.4, 11, 9.6, 12.8, 13.8, 12.3, 10.2, 14.3, 16],
  byStatus: [
    { status: 'Confirmed', count: 12 }, { status: 'Processing', count: 8 }, { status: 'Dispatched', count: 5 },
    { status: 'Delivered', count: 17 }, { status: 'Cancelled', count: 4 },
  ],
  totalOrders: 46,
  topProducts: [
    { name: 'Executive High-Back Leather Chair', sold: 14 }, { name: 'Three-Seater Fabric Sofa', sold: 9 }, { name: 'Study Desk', sold: 7 },
  ],
  languages: [{ label: 'Sinhala', pct: 52 }, { label: 'English', pct: 31 }, { label: 'Tamil', pct: 17 }],
  languagesSimulated: true,
}

export const demoConversations: Conversation[] = [
  {
    id: 1, customerId: 1, name: 'Kasun D.', phone: '94700000001', time: '10:01', preview: 'chair ekak denna', orderId: 41,
    messages: [
      { kind: 'in', text: 'chair ekak denna' },
      { kind: 'ai', lang: 'SI', intent: 'Order', confidence: 0.96 },
      { kind: 'out', title: 'Order Confirmed!', lines: ['ORD-2026-041', 'Executive High-Back Leather Chair × 1', '**Total LKR 28,500**'] },
      { kind: 'event', text: 'Order created · stock 20 → 19', tone: 'green' },
      { kind: 'in', text: 'Slip sent' },
      { kind: 'out', title: 'Payment slip received', lines: ['Our team will verify and confirm shortly.'] },
      { kind: 'event', text: 'Payment → Pending review', tone: 'blue' },
    ],
    analysis: { language: 'Sinhala (romanised)', intent: 'Order', confidence: 0.96, productMatch: 'Executive Chair', decidedBy: 'Code' },
  },
  {
    id: 2, customerId: 4, name: 'Priya S.', phone: '94700000002', time: '9:40', preview: 'எனக்கு sofa வேண்டும்', previewLang: 'ta', orderId: 37,
    messages: [
      { kind: 'in', text: 'எனக்கு sofa வேண்டும்', lang: 'ta' },
      { kind: 'ai', lang: 'TA', intent: 'ProductSearch', confidence: 0.94 },
      { kind: 'out', title: '1 பொருந்திய பொருட்கள்:', lines: ['Three-Seater Fabric Sofa', 'LKR 55,000 · In stock'] },
    ],
    analysis: { language: 'Tamil', intent: 'ProductSearch', confidence: 0.94, productMatch: 'Three-Seater Fabric Sofa', decidedBy: 'Code' },
  },
  {
    id: 3, customerId: 2, name: 'Ayesha F.', phone: '94700000003', time: '9:12', preview: 'Can I get a better price?', orderId: 42,
    messages: [
      { kind: 'in', text: 'Can I get the fabric sofa for 42,000?' },
      { kind: 'ai', lang: 'EN', intent: 'PriceNegotiation', confidence: 0.92 },
      { kind: 'out', lines: ['Best we can do for Three-Seater Fabric Sofa is LKR 45,000 each — that\'s our lowest price.', 'Reply *confirm* to accept this price.'] },
      { kind: 'in', text: 'confirm' },
      { kind: 'out', title: 'Order Confirmed!', lines: ['ORD-2026-042', 'Three-Seater Fabric Sofa × 1', '**Total LKR 45,000**'] },
      { kind: 'event', text: 'Order created · negotiated 45,000 (≥ min price)', tone: 'green' },
      { kind: 'in', text: 'Can I get a better price?' },
    ],
    analysis: { language: 'English', intent: 'PriceNegotiation', confidence: 0.92, productMatch: 'Three-Seater Fabric Sofa', decidedBy: 'Code' },
  },
  {
    id: 4, customerId: 3, name: 'Nimali P.', phone: '94700000004', time: 'Yesterday', preview: 'slip eka evvaa', orderId: 40,
    messages: [
      { kind: 'in', text: 'slip eka evvaa' },
      { kind: 'ai', lang: 'SI', intent: 'PaymentConfirmation', confidence: 0.9 },
      { kind: 'out', title: 'Payment slip received', lines: ['Our team will verify and confirm shortly.'] },
      { kind: 'event', text: 'Payment → Pending review', tone: 'blue' },
    ],
    analysis: { language: 'Sinhala (romanised)', intent: 'PaymentConfirmation', confidence: 0.9, productMatch: '—', decidedBy: 'Code' },
  },
]

export const demoDocuments: DocumentRow[] = [
  { id: 1, kind: 'Invoice', orderNumber: 'ORD-2026-042', customer: 'Ayesha F.', date: iso('2026-11-10T10:00'), items: [{ name: 'Three-Seater Fabric Sofa', qty: 1, total: 45000 }], total: 45000 },
  { id: 2, kind: 'Invoice', orderNumber: 'ORD-2026-041', customer: 'Kasun D.', date: iso('2026-11-10T08:00'), items: [{ name: 'Executive High-Back Leather Chair', qty: 1, total: 28500 }], total: 28500 },
  { id: 3, kind: 'Receipt', orderNumber: 'ORD-2026-040', customer: 'Nimali P.', date: iso('2026-11-09T16:00'), items: [{ name: 'Three-Seater Fabric Sofa', qty: 1, total: 55000 }], total: 55000 },
  { id: 4, kind: 'Receipt', orderNumber: 'ORD-2026-037', customer: 'Priya S.', date: iso('2026-11-08T12:00'), items: [{ name: 'Dining Table Set', qty: 1, total: 18500 }], total: 18500 },
]
