/**
 * Status vocabulary shown in the UI, and the mapping from backend enum strings.
 * Order statuses the backend actually sets: Confirmed, Processing, Dispatched, Delivered, Cancelled.
 * Payment statuses it sets: Unpaid, PendingReview, Paid.
 * "Pending" (order) and "Refunded" (payment) exist in the backend enums but are never set by
 * backend code; the design draws them dashed. FraudPending is never shown (no fraud badge).
 */
export const ORDER_STATUSES = ['Confirmed', 'Processing', 'Dispatched', 'Delivered', 'Cancelled'] as const
export type OrderStatus = (typeof ORDER_STATUSES)[number]

export const PAYMENT_STATUSES = ['Unpaid', 'Pending review', 'Paid'] as const
export type PaymentStatus = (typeof PAYMENT_STATUSES)[number]

/** Statuses that exist in backend enums but are never set by backend code. */
export const NEVER_SET_STATUSES = ['Pending', 'Refunded'] as const
export type NeverSetStatus = (typeof NEVER_SET_STATUSES)[number]

export type AnyStatus = OrderStatus | PaymentStatus | NeverSetStatus

export type BadgeTone =
  | 'confirmed' | 'processing' | 'dispatched' | 'delivered' | 'cancelled'
  | 'unpaid' | 'review' | 'paid' | 'pending' | 'refunded'

const TONES: Record<AnyStatus, BadgeTone> = {
  Confirmed: 'confirmed',
  Processing: 'processing',
  Dispatched: 'dispatched',
  Delivered: 'delivered',
  Cancelled: 'cancelled',
  Unpaid: 'unpaid',
  'Pending review': 'review',
  Paid: 'paid',
  Pending: 'pending',
  Refunded: 'refunded',
}

export function toneFor(status: AnyStatus): BadgeTone {
  return TONES[status]
}

export function isNeverSet(status: AnyStatus): status is NeverSetStatus {
  return (NEVER_SET_STATUSES as readonly string[]).includes(status)
}

/** Backend OrderStatus enum string -> UI order status. Returns null for values the UI never shows. */
export function mapOrderStatus(api: string): OrderStatus | null {
  return (ORDER_STATUSES as readonly string[]).includes(api) ? (api as OrderStatus) : null
}

/** Backend PaymentStatus enum string -> UI payment status. Returns null for values the UI never shows. */
export function mapPaymentStatus(api: string): PaymentStatus | null {
  switch (api) {
    case 'Unpaid': return 'Unpaid'
    case 'PendingReview': return 'Pending review'
    case 'Paid': return 'Paid'
    default: return null
  }
}

/** Valid fulfilment moves (mirrors DeliveryService.IsValidTransition, non-cancel paths). */
export function nextFulfilmentStatus(current: OrderStatus): OrderStatus | null {
  switch (current) {
    case 'Confirmed': return 'Processing'
    case 'Processing': return 'Dispatched'
    case 'Dispatched': return 'Delivered'
    default: return null
  }
}

export const FULFILMENT_STEPS: readonly OrderStatus[] = ['Confirmed', 'Processing', 'Dispatched', 'Delivered']
