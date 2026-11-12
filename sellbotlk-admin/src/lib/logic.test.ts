import assert from 'node:assert/strict'
import { test } from 'node:test'
import { demoOrders } from '../data/demo'
import { compactK, hoursUntilExpiry, lkr, maskPhone } from './format'
import { decideOffer } from './negotiation'
import { expiryOf } from './orders'
import {
  isNeverSet, mapOrderStatus, mapPaymentStatus, nextFulfilmentStatus, ORDER_STATUSES, PAYMENT_STATUSES, toneFor,
} from './status'

test('backend order statuses map to the five UI statuses; FraudPending/Pending are never shown', () => {
  for (const s of ORDER_STATUSES) assert.equal(mapOrderStatus(s), s)
  assert.equal(mapOrderStatus('FraudPending'), null)
  assert.equal(mapOrderStatus('Pending'), null)
})

test('backend payment statuses map to Unpaid / Pending review / Paid; Refunded is never shown', () => {
  assert.equal(mapPaymentStatus('Unpaid'), 'Unpaid')
  assert.equal(mapPaymentStatus('PendingReview'), 'Pending review')
  assert.equal(mapPaymentStatus('Paid'), 'Paid')
  assert.equal(mapPaymentStatus('Refunded'), null)
  assert.deepEqual([...PAYMENT_STATUSES], ['Unpaid', 'Pending review', 'Paid'])
})

test('Pending and Refunded are the only never-set (dashed) statuses', () => {
  assert.ok(isNeverSet('Pending') && isNeverSet('Refunded'))
  for (const s of [...ORDER_STATUSES, ...PAYMENT_STATUSES]) assert.equal(isNeverSet(s), false)
  assert.equal(toneFor('Pending review'), 'review')
})

test('fulfilment only moves forward one step at a time (mirrors DeliveryService)', () => {
  assert.equal(nextFulfilmentStatus('Confirmed'), 'Processing')
  assert.equal(nextFulfilmentStatus('Processing'), 'Dispatched')
  assert.equal(nextFulfilmentStatus('Dispatched'), 'Delivered')
  assert.equal(nextFulfilmentStatus('Delivered'), null)
  assert.equal(nextFulfilmentStatus('Cancelled'), null)
})

test('formatting matches the design', () => {
  assert.equal(lkr(45000), 'LKR 45,000')
  assert.equal(lkr(1284000), 'LKR 1,284,000')
  assert.equal(maskPhone('94700000003'), '+94 70 ••• ••03')
  assert.equal(compactK(71200), '71.2k')
  assert.equal(compactK(71200 / 3), '23.7k')
})

test('expiry column: 24 h window from the last update, red at 6 h or less', () => {
  const now = new Date('2026-11-10T10:30:00')
  assert.equal(hoursUntilExpiry('2026-11-10T07:30:00', now), 21)
  assert.deepEqual(expiryOf({ payment: 'Unpaid', status: 'Confirmed', updatedAt: '2026-11-10T07:30:00' }, now), { text: 'Expires in 21 h', tone: 'amber' })
  assert.deepEqual(expiryOf({ payment: 'Unpaid', status: 'Confirmed', updatedAt: '2026-11-09T13:30:00' }, now), { text: 'Expires in 3 h', tone: 'red' })
  assert.equal(expiryOf({ payment: 'Pending review', status: 'Confirmed', updatedAt: '2026-11-10T07:30:00' }, now).text, 'Slip under review')
  assert.equal(expiryOf({ payment: 'Unpaid', status: 'Cancelled', updatedAt: '2026-11-05T09:00:00' }, now).text, 'Expired')
  assert.equal(expiryOf({ payment: 'Paid', status: 'Processing', updatedAt: '2026-11-09T15:10:00' }, now).text, '—')
})

test('negotiation mirror follows NegotiationService: accept / counter in 10% band / reject', () => {
  // Three-Seater Fabric Sofa as in the backend seed: list 55,000, min 45,000
  assert.deepEqual(decideOffer(55000, 45000, 45000), { outcome: 'accept', price: 45000 })
  assert.deepEqual(decideOffer(55000, 45000, 60000), { outcome: 'accept', price: 55000 }) // never above list
  assert.deepEqual(decideOffer(55000, 45000, 42000), { outcome: 'counter', price: 45000 })
  assert.deepEqual(decideOffer(55000, 45000, 40500), { outcome: 'counter', price: 45000 })
  assert.deepEqual(decideOffer(55000, 45000, 40000), { outcome: 'reject' })
})

test('demo fixtures hold the counts drawn in the design', () => {
  const count = (s: string) => demoOrders.filter((o) => o.status === s).length
  assert.equal(demoOrders.length, 46)
  assert.deepEqual(ORDER_STATUSES.map(count), [12, 8, 5, 17, 4])
  assert.equal(demoOrders.filter((o) => o.payment === 'Pending review').length, 3)
  assert.deepEqual(demoOrders.slice(0, 7).map((o) => o.number), [
    'ORD-2026-042', 'ORD-2026-041', 'ORD-2026-040', 'ORD-2026-039', 'ORD-2026-037', 'ORD-2026-035', 'ORD-2026-033',
  ])
  assert.equal(new Set(demoOrders.map((o) => o.number)).size, 46)
})
