export function StatCard(props: {
  label: string
  value: string
  sub?: string
  tone?: 'neutral' | 'brand' | 'good' | 'warn' | 'bad'
}) {
  const tone = props.tone ?? 'neutral'
  return (
    <div className={`statCard ${tone}`}>
      <div className="statLabel">{props.label}</div>
      <div className="statValue">{props.value}</div>
      {props.sub && <div className="statSub">{props.sub}</div>}
    </div>
  )
}
