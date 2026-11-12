import type { Order } from '../data/types'
import { hoursUntilExpiry } from './format'

export type Expiry = { text: string; tone: 'muted' | 'amber' | 'red' | 'dash' }

/** "Expires" column: slip under review, unpaid countdown (red at 6 h or less), expired, or a dash. */
export function expiryOf(o: Pick<Order, 'payment' | 'status' | 'updatedAt'>, now: Date): Expiry {
  if (o.payment === 'Pending review') return { text: 'Slip under review', tone: 'muted' }
  if (o.payment === 'Unpaid' && o.status === 'Cancelled') return { text: 'Expired', tone: 'muted' }
  if (o.payment === 'Unpaid' && o.status === 'Confirmed') {
    const h = hoursUntilExpiry(o.updatedAt, now)
    return { text: `Expires in ${h} h`, tone: h <= 6 ? 'red' : 'amber' }
  }
  return { text: '—', tone: 'dash' }
}
