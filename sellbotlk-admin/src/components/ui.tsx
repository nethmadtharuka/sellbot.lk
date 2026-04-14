import type { PropsWithChildren, ReactNode } from 'react'

export function Card(props: PropsWithChildren<{ title?: ReactNode; right?: ReactNode }>) {
  return (
    <section className="card">
      {(props.title || props.right) && (
        <div className="cardHeader">
          <div className="cardTitle">{props.title}</div>
          <div className="cardRight">{props.right}</div>
        </div>
      )}
      <div className="cardBody">{props.children}</div>
    </section>
  )
}

export function Badge(props: PropsWithChildren<{ tone?: 'neutral' | 'good' | 'warn' | 'bad' }>) {
  const tone = props.tone ?? 'neutral'
  return <span className={`badge ${tone}`}>{props.children}</span>
}

export function Button(
  props: PropsWithChildren<
    React.ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'secondary' | 'danger' }
  >,
) {
  const { variant = 'primary', className, ...rest } = props
  return (
    <button {...rest} className={`btn ${variant} ${className ?? ''}`.trim()}>
      {props.children}
    </button>
  )
}

export function Input(props: React.InputHTMLAttributes<HTMLInputElement>) {
  const { className, ...rest } = props
  return <input {...rest} className={`input ${className ?? ''}`.trim()} />
}

export function Select(props: React.SelectHTMLAttributes<HTMLSelectElement>) {
  const { className, ...rest } = props
  return <select {...rest} className={`select ${className ?? ''}`.trim()} />
}

export function Field(props: PropsWithChildren<{ label: string; hint?: string }>) {
  return (
    <label className="field">
      <div className="fieldLabel">{props.label}</div>
      {props.children}
      {props.hint && <div className="fieldHint">{props.hint}</div>}
    </label>
  )
}

export function Table(props: PropsWithChildren<{}>) {
  return <div className="tableWrap">{props.children}</div>
}

