import type { OrderStatus, PaymentStatus } from '../lib/status'

export type Lang = 'en' | 'si' | 'ta'

export type OrderItem = {
  name: string
  qty: number
  unitPrice: number
  negotiatedPrice?: number
  minPrice?: number
}

export type Order = {
  id: number
  number: string
  customerId?: number
  customerName: string
  customerPhone: string
  lang?: Lang
  createdAt: string
  updatedAt: string
  /** Source channel. Not stored by the backend, so live orders leave it undefined. */
  channel?: 'WhatsApp' | 'Admin'
  items: OrderItem[]
  total: number
  status: OrderStatus
  payment: PaymentStatus
  /** Time the slip arrived (payments queue). */
  slipReceived?: string
}

export type Product = {
  id: number
  name: string
  /** Short label used in compact lists (tablet overview). */
  shortName?: string
  category: string
  description?: string
  price: number
  /** Internal floor. Not returned by the live API (ProductResponseDto omits it). */
  minPrice?: number
  stock: number
  lowStockThreshold: number
  active: boolean
}

export type Customer = {
  id: number
  name: string
  phone: string
  lang: Lang
  orders: number
  lifetime: number
  lastOrder: string
  since: string
}

export type ChatMessage =
  | { kind: 'in'; text: string; lang?: Lang }
  | { kind: 'out'; title?: string; lines: string[] }
  | { kind: 'event'; text: string; tone: 'green' | 'blue' }
  | { kind: 'ai'; lang: string; intent: string; confidence: number }

export type Conversation = {
  id: number
  customerId: number
  name: string
  phone: string
  time: string
  preview: string
  previewLang?: Lang
  orderId?: number
  messages: ChatMessage[]
  analysis: { language: string; intent: string; confidence: number; productMatch: string; decidedBy: string }
}

export type DocumentRow = {
  id: number
  kind: 'Invoice' | 'Receipt'
  orderNumber: string
  customer: string
  date: string
  items: { name: string; qty: number; total: number }[]
  total: number
}

export type Overview = {
  revenue30: number
  revenueOrders: number
  orders30: number
  ordersBreakdown: string
  paymentsToReview: number
  oldestWaiting: string
  lowStock: number
  queues: { count: number; tone: 'blue' | 'amber' | 'red'; title: string; detail: string; action: string; to: string }[]
  revenue14: { label: string; value: number }[]
}

export type Analytics = {
  ordersPerDay: number[]
  byStatus: { status: OrderStatus; count: number }[]
  totalOrders: number
  topProducts: { name: string; sold: number }[]
  languages: { label: string; pct: number }[]
  /** The backend does not aggregate customer language, so this panel is always mock data. */
  languagesSimulated: boolean
}

/** Which data a page shows came from the mock layer even in live mode. */
export type Sourced<T> = { data: T; simulated: boolean }
