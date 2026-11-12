import type { OrderStatus } from '../lib/status'
import type { Analytics, Conversation, Customer, DocumentRow, Order, Overview, Product, Sourced } from './types'

/**
 * Everything a page can read or change. Pages call only this interface, never fetch().
 * `simulated: true` on a result means it came from the mock layer (always true in demo
 * mode; in live mode only where the backend has no endpoint).
 */
export interface DataSource {
  readonly mode: 'demo' | 'live'
  overview(): Promise<Sourced<Overview>>
  orders(): Promise<Sourced<Order[]>>
  order(id: number): Promise<Sourced<Order | null>>
  advanceOrder(id: number, next: OrderStatus): Promise<void>
  cancelOrder(id: number): Promise<void>
  approvePayment(id: number): Promise<void>
  rejectPayment(id: number, reason?: string): Promise<void>
  /** Object URL for the submitted slip, or null when there is no image (demo shows a placeholder). */
  slipImage(id: number): Promise<string | null>
  products(): Promise<Sourced<Product[]>>
  saveProduct(product: Product): Promise<void>
  customers(): Promise<Sourced<Customer[]>>
  customer(id: number): Promise<Sourced<{ customer: Customer; orders: Order[] } | null>>
  conversations(): Promise<Sourced<Conversation[]>>
  analytics(): Promise<Sourced<Analytics>>
  documents(): Promise<Sourced<DocumentRow[]>>
}

export class NotFoundError extends Error {}
