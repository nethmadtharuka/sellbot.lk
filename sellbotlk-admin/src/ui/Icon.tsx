import { lucide, whatsappPath, type IconName } from './iconData'

type Props = { name: IconName; size?: number; strokeWidth?: number; className?: string }

/** Lucide line icon. Markup comes from the generated, static iconData module (trusted). */
export function Icon({ name, size = 20, strokeWidth = 1.6, className }: Props) {
  return (
    <svg
      className={className}
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      dangerouslySetInnerHTML={{ __html: lucide[name] }}
    />
  )
}

/** Simple Icons WhatsApp glyph, path unmodified. */
export function WhatsAppGlyph({ size = 22, className }: { size?: number; className?: string }) {
  return (
    <svg className={className} width={size} height={size} viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" focusable="false">
      <path d={whatsappPath} />
    </svg>
  )
}
