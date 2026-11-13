/**
 * A number that may count up. The final value is always in the DOM: a screen-reader copy plus the
 * visible aria-hidden span that motion code rolls (it ends on the same value).
 */
export function Count({ value }: { value: number }) {
  return (
    <span className="count" data-count={value}>
      <span className="count__roll" aria-hidden="true">{value}</span>
      <span className="sr-only">{value}</span>
    </span>
  )
}
