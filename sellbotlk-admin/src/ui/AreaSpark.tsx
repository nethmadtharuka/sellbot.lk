import { useId } from 'react'

/**
 * Revenue area chart as drawn on Overview: straight segments, 2.5 px mint line, green fill
 * fading to transparent. Plain SVG (no measuring), so it renders the same hidden or visible.
 */
export function AreaSpark({ values, label }: { values: number[]; label: string }) {
  const gid = useId()
  if (values.length < 2) return null
  const W = 1000, H = 100
  const min = Math.min(...values), max = Math.max(...values)
  const lo = min - (max - min) * 0.45 // headroom below the lowest point, as in the design
  const x = (i: number) => (i / (values.length - 1)) * W
  const y = (v: number) => H - ((v - lo) / (max - lo || 1)) * (H - 2) - 1
  const line = values.map((v, i) => `${x(i).toFixed(1)},${y(v).toFixed(1)}`).join(' ')
  return (
    <svg className="area-spark" viewBox={`0 0 ${W} ${H}`} preserveAspectRatio="none" role="img" aria-label={label}>
      <defs>
        <linearGradient id={gid} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#25d366" stopOpacity="0.55" />
          <stop offset="100%" stopColor="#25d366" stopOpacity="0" />
        </linearGradient>
      </defs>
      <polygon points={`0,${H} ${line} ${W},${H}`} fill={`url(#${gid})`} />
      <polyline points={line} fill="none" stroke="#6deb9f" strokeWidth="2.5" vectorEffect="non-scaling-stroke" strokeLinejoin="round" />
    </svg>
  )
}
