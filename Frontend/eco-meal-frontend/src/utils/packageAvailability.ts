// Stock minus other customers' pending reservations minus what's already in *this* viewer's own
// basket, never negative.
export function availableQuantity(stockQuantity: number, reservedElsewhere: number, inBasket = 0): number {
  return Math.max(0, stockQuantity - reservedElsewhere - inBasket);
}
