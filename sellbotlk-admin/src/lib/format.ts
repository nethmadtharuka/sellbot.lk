/** LKR amounts are shown as "LKR 45,000" (backend uses N0). */
export function lkr(amount: number): string {
  return `LKR ${Math.round(amount).toLocaleString('en-US')}`
}

/** Compact amounts used on customer KPI tiles: 71200 -> "71.2k". */
export function compactK(amount: number): string {
  if (amount < 1000) return String(Math.round(amount))
  const k = amount / 1000
  return `${Number.isInteger(k) ? k : k.toFixed(1)}k`
}

/** "+94 70 ••• ••01": country + operator prefix, then the last two digits only. */
export function maskPhone(phone: string): string {
  const digits = phone.replace(/\D/g, '')
  if (digits.length < 6) return '•••'
  return `+${digits.slice(0, 2)} ${digits.slice(2, 4)} ••• ••${digits.slice(-2)}`
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

/** "10 Nov 2026" */
export function shortDate(iso: string): string {
  const d = new Date(iso)
  return `${d.getDate()} ${MONTHS[d.getMonth()]} ${d.getFullYear()}`
}

/** "10 Nov" */
export function dayMonth(iso: string): string {
  const d = new Date(iso)
  return `${d.getDate()} ${MONTHS[d.getMonth()]}`
}

/** Hours left before an unpaid order is auto-cancelled (backend default 24 h, from UpdatedAt). */
export function hoursUntilExpiry(updatedAtIso: string, now: Date, windowHours = 24): number {
  const ms = new Date(updatedAtIso).getTime() + windowHours * 3_600_000 - now.getTime()
  return Math.max(0, Math.ceil(ms / 3_600_000))
}

export function initial(name: string): string {
  return name.trim().charAt(0).toUpperCase()
}
