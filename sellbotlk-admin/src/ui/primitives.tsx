import { useEffect, useId, useRef, type ButtonHTMLAttributes, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { isNeverSet, toneFor, type AnyStatus } from '../lib/status'
import type { Lang } from '../data/types'
import { Icon } from './Icon'

/* ---------------------------------------------------------------- Button */
export type ButtonVariant = 'green' | 'white' | 'dark' | 'outline' | 'danger' | 'ghost'
export type ButtonSize = 'sm' | 'md' | 'lg'
type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant
  size?: ButtonSize
  /** Renders the control disabled but focusable, with an explanatory tooltip. */
  disabledReason?: string
}
export function Button({ variant = 'dark', size = 'md', disabledReason, className, onClick, children, ...rest }: ButtonProps) {
  const off = Boolean(disabledReason)
  return (
    <button
      type="button"
      {...rest}
      className={`btn btn--${variant} btn--${size}${off ? ' is-disabled' : ''} ${className ?? ''}`.trim()}
      aria-disabled={off || undefined}
      data-tooltip={disabledReason}
      onClick={off ? (e) => e.preventDefault() : onClick}
    >
      {children}
    </button>
  )
}
export function ButtonLink({ to, variant = 'dark', size = 'md', className, children, ...rest }: {
  to: string; variant?: ButtonVariant; size?: ButtonSize; className?: string; children: ReactNode; 'aria-label'?: string
}) {
  return <Link to={to} className={`btn btn--${variant} btn--${size} ${className ?? ''}`.trim()} {...rest}>{children}</Link>
}

/* ---------------------------------------------------------------- Badges & chips */
export function StatusBadge({ status }: { status: AnyStatus }) {
  const never = isNeverSet(status)
  return (
    <span
      className={`badge badge--${toneFor(status)}${never ? ' badge--never' : ''}`}
      title={never ? 'Never set by backend code' : undefined}
    >
      {status}
    </span>
  )
}
export type ChipTone = 'neutral' | 'violet' | 'green' | 'tag' | 'blue'
export function Chip({ tone = 'neutral', children, lang }: { tone?: ChipTone; children: ReactNode; lang?: string }) {
  return <span className={`chip chip--${tone}`} lang={lang}>{children}</span>
}
const LANG_LABEL: Record<Lang, { short: string; long: string; native: string }> = {
  en: { short: 'EN', long: 'English', native: 'English' },
  si: { short: 'SI', long: 'Sinhala', native: 'සිංහල' },
  ta: { short: 'TA', long: 'Tamil', native: 'தமிழ்' },
}
/** Language chip. `native` shows the script name in its own script, as on the Customers table. */
export function LangChip({ lang, variant = 'short' }: { lang: Lang; variant?: 'short' | 'native' | 'long' }) {
  const l = LANG_LABEL[lang]
  if (variant === 'native') {
    return <Chip tone={lang === 'en' ? 'neutral' : 'violet'} lang={lang}>{l.native}</Chip>
  }
  return <span className="chip chip--lang" title={l.long}>{variant === 'long' ? l.long : l.short}</span>
}
export function Mono({ children, className }: { children: ReactNode; className?: string }) {
  return <span className={`mono ${className ?? ''}`.trim()}>{children}</span>
}

/* ---------------------------------------------------------------- Layout pieces */
export function Card({ children, className, as: Tag = 'section', ...rest }: {
  children: ReactNode; className?: string; as?: 'section' | 'div' | 'article' | 'aside'; 'aria-labelledby'?: string; 'aria-label'?: string; 'data-anim'?: string
}) {
  return <Tag className={`card ${className ?? ''}`.trim()} {...rest}>{children}</Tag>
}
export function PageHeader({ eyebrow, title, right, crumb }: { eyebrow?: string; crumb?: ReactNode; title: ReactNode; right?: ReactNode }) {
  return (
    <header className="page-head" data-anim="page-head">
      <div>
        {(eyebrow || crumb) && <p className="eyebrow">{crumb ?? eyebrow}</p>}
        <h1 className="page-title">{title}</h1>
      </div>
      {right && <div className="page-head__right">{right}</div>}
    </header>
  )
}

/* ---------------------------------------------------------------- Form controls */
export function Field({ label, hint, children, id, hintTone }: { label: string; hint?: ReactNode; children: (id: string) => ReactNode; id?: string; hintTone?: 'violet' }) {
  const auto = useId()
  const fid = id ?? auto
  return (
    <div className="field">
      <label className="field__label" htmlFor={fid}>{label}</label>
      {children(fid)}
      {hint && <p className={`field__hint${hintTone ? ` field__hint--${hintTone}` : ''}`}>{hint}</p>}
    </div>
  )
}
export function SearchField({ label, placeholder, value, onChange, className }: {
  label: string; placeholder: string; value: string; onChange: (v: string) => void; className?: string
}) {
  return (
    <label className={`search ${className ?? ''}`.trim()}>
      <span className="sr-only">{label}</span>
      <Icon name="search" size={18} />
      <input type="search" placeholder={placeholder} value={value} onChange={(e) => onChange(e.target.value)} />
    </label>
  )
}
export function Toggle({ checked, onChange, label }: { checked: boolean; onChange: (v: boolean) => void; label: string }) {
  return (
    <button type="button" role="switch" aria-checked={checked} className={`toggle${checked ? ' is-on' : ''}`} onClick={() => onChange(!checked)}>
      <span className="toggle__label">{label}</span>
      <span className="toggle__track" aria-hidden="true"><span className="toggle__thumb" /></span>
    </button>
  )
}

/* ---------------------------------------------------------------- Tabs & filter chips */
export function PillTabs<T extends string>({ items, value, onChange, label }: {
  items: { value: T; label: string; count?: number }[]; value: T; onChange: (v: T) => void; label: string
}) {
  return (
    <div className="pill-tabs" role="tablist" aria-label={label}
      onKeyDown={(e) => {
        if (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft') return
        const i = items.findIndex((x) => x.value === value)
        const next = items[(i + (e.key === 'ArrowRight' ? 1 : items.length - 1)) % items.length]
        onChange(next.value)
        ;(e.currentTarget.querySelector(`[data-value="${next.value}"]`) as HTMLElement | null)?.focus()
      }}>
      {items.map((t) => (
        <button key={t.value} type="button" role="tab" data-value={t.value} aria-selected={t.value === value}
          tabIndex={t.value === value ? 0 : -1} className={`pill-tab${t.value === value ? ' is-active' : ''}`} onClick={() => onChange(t.value)}>
          {t.label}
          {t.count !== undefined && <span className="pill-tab__count">{t.count}</span>}
        </button>
      ))}
    </div>
  )
}
export function FilterChip({ tone, pressed, onClick, children }: { tone: string; pressed: boolean; onClick: () => void; children: ReactNode }) {
  return <button type="button" aria-pressed={pressed} className={`filter-chip filter-chip--${tone}${pressed ? ' is-on' : ''}`} onClick={onClick}>{children}</button>
}

/* ---------------------------------------------------------------- Feedback */
export function Skeleton({ rows = 3 }: { rows?: number }) {
  return (
    <div className="skeleton" role="status" aria-label="Loading">
      {Array.from({ length: rows }, (_, i) => (
        <div className="skeleton__row" key={i}>
          <span className="skeleton__dot" /><span className="skeleton__bar skeleton__bar--l" /><span className="skeleton__bar skeleton__bar--m" /><span className="skeleton__bar skeleton__bar--s" />
        </div>
      ))}
    </div>
  )
}
export function EmptyState({ title, body, action }: { title: string; body: string; action?: ReactNode }) {
  return (
    <div className="empty">
      <span className="empty__icon"><Icon name="shopping-bag" size={22} /></span>
      <p className="empty__title">{title}</p>
      <p className="empty__body">{body}</p>
      {action}
    </div>
  )
}
export function InlineError({ title, body, onRetry }: { title: string; body: string; onRetry?: () => void }) {
  return (
    <div className="inline-error" role="alert">
      <Icon name="x" size={18} className="inline-error__x" />
      <div className="inline-error__text"><p className="inline-error__title">{title}</p><p>{body}</p></div>
      {onRetry && <Button variant="danger" onClick={onRetry}>Retry</Button>}
    </div>
  )
}
export function Toast({ message, onDone }: { message: string; onDone: () => void }) {
  useEffect(() => { const t = setTimeout(onDone, 4000); return () => clearTimeout(t) }, [onDone])
  return (
    <div className="toast" role="status" aria-live="polite">
      <Icon name="check" size={18} className="toast__check" />{message}
    </div>
  )
}
export function ModeBanner({ mode, compact }: { mode: 'demo' | 'live'; compact?: boolean }) {
  return (
    <div className={`mode-banner mode-banner--${mode}`} role="note">
      <span className="mode-banner__pill">{mode === 'demo' ? 'DEMO' : 'LIVE'}</span>
      <span>{mode === 'demo'
        ? (compact ? 'Fictional data' : 'Fictional data · no real messages are sent · changes reset on reload')
        : (compact ? 'Real business' : 'Connected to the real business · actions affect real customers')}</span>
    </div>
  )
}
export function Avatar({ name, size = 'md' }: { name: string; size?: 'sm' | 'md' | 'lg' }) {
  return <span className={`avatar avatar--${size}`} aria-hidden="true">{name.trim().charAt(0).toUpperCase()}</span>
}
export function Meter({ value, max, tone = 'green', label }: { value: number; max: number; tone?: 'green' | 'blue' | 'violet' | 'teal' | 'red' | 'amber'; label: string }) {
  const pct = max > 0 ? Math.max(0, Math.min(100, (value / max) * 100)) : 0
  return (
    <span className="meter" role="meter" aria-label={label} aria-valuemin={0} aria-valuemax={max} aria-valuenow={value}>
      <span className={`meter__fill meter__fill--${tone}`} style={{ width: `${pct}%` }} />
    </span>
  )
}

/* ---------------------------------------------------------------- Overlays */
function useDialog(open: boolean, onClose: () => void) {
  const ref = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (!open) return
    const prev = document.activeElement as HTMLElement | null
    const root = ref.current
    const focusables = () => [...(root?.querySelectorAll<HTMLElement>('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])') ?? [])]
    ;(root?.querySelector<HTMLElement>('input, select, textarea') ?? focusables()[0])?.focus()
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { e.preventDefault(); onClose() }
      if (e.key !== 'Tab') return
      const f = focusables(); if (!f.length) return
      const first = f[0], last = f[f.length - 1]
      if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus() }
      else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus() }
    }
    document.addEventListener('keydown', onKey)
    return () => { document.removeEventListener('keydown', onKey); prev?.focus() }
  }, [open, onClose])
  return ref
}
export function ConfirmModal({ open, title, body, confirmLabel, cancelLabel, onConfirm, onClose, danger = true, children }: {
  open: boolean; title: string; body: string; confirmLabel: string; cancelLabel: string; onConfirm: () => void; onClose: () => void; danger?: boolean; children?: ReactNode
}) {
  const ref = useDialog(open, onClose)
  const tid = useId()
  if (!open) return null
  return (
    <div className="overlay" onMouseDown={(e) => { if (e.target === e.currentTarget) onClose() }}>
      <div className="modal" role="dialog" aria-modal="true" aria-labelledby={tid} ref={ref}>
        <h2 className="modal__title" id={tid}>{title}</h2>
        <p className="modal__body">{body}</p>
        {children && <div className="modal__extra">{children}</div>}
        <div className="modal__actions">
          <Button variant={danger ? 'danger' : 'green'} onClick={onConfirm}>{confirmLabel}</Button>
          <Button variant="dark" onClick={onClose}>{cancelLabel}</Button>
        </div>
      </div>
    </div>
  )
}
export function Drawer({ open, title, onClose, children, footer }: { open: boolean; title: string; onClose: () => void; children: ReactNode; footer: ReactNode }) {
  const ref = useDialog(open, onClose)
  const tid = useId()
  if (!open) return null
  return (
    <div className="overlay overlay--drawer" onMouseDown={(e) => { if (e.target === e.currentTarget) onClose() }}>
      <div className="drawer" role="dialog" aria-modal="true" aria-labelledby={tid} ref={ref}>
        <div className="drawer__head">
          <h2 className="drawer__title" id={tid}>{title}</h2>
          <button type="button" className="icon-btn" aria-label="Close" data-tooltip="Close" onClick={onClose}><Icon name="x" size={22} /></button>
        </div>
        <div className="drawer__body">{children}</div>
        <div className="drawer__foot">{footer}</div>
      </div>
    </div>
  )
}
