/**
 * Mirror of NegotiationService.EvaluateOfferAsync (SellBotLk.Api/Services/NegotiationService.cs),
 * without the bulk-discount branch: offer >= min price -> accept at min(offer, list);
 * offer within 10% below min -> counter at min; lower -> reject.
 */
export type Decision =
  | { outcome: 'accept'; price: number }
  | { outcome: 'counter'; price: number }
  | { outcome: 'reject' }

export const COUNTER_BAND = 0.1

export function decideOffer(listPrice: number, minPrice: number, offer: number): Decision {
  if (offer >= minPrice) return { outcome: 'accept', price: Math.min(offer, listPrice) }
  if (offer >= minPrice * (1 - COUNTER_BAND)) return { outcome: 'counter', price: minPrice }
  return { outcome: 'reject' }
}

export function decisionLabel(d: Decision): string {
  switch (d.outcome) {
    case 'accept': return 'Accept: offer ≥ min price'
    case 'counter': return 'Counter at min price: offer within 10% below it'
    case 'reject': return 'Reject: offer too far below min price'
  }
}
