import assert from 'node:assert/strict'
import { test } from 'node:test'
import type { ReactElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { MemoryRouter } from 'react-router-dom'
import { Button, Chip, InlineError, LangChip, ModeBanner, StatusBadge } from './primitives'

const html = (el: ReactElement) => renderToStaticMarkup(<MemoryRouter>{el}</MemoryRouter>)

test('StatusBadge uses the tone class and draws never-set statuses dashed', () => {
  assert.match(html(<StatusBadge status="Pending review" />), /class="badge badge--review"/)
  assert.match(html(<StatusBadge status="Refunded" />), /badge--refunded badge--never/)
  assert.match(html(<StatusBadge status="Pending" />), /title="Never set by backend code"/)
  assert.doesNotMatch(html(<StatusBadge status="Paid" />), /badge--never/)
})

test('disabled-with-reason buttons stay focusable and explain why', () => {
  const out = html(<Button disabledReason="Disabled in demo">Track order</Button>)
  assert.match(out, /aria-disabled="true"/)
  assert.match(out, /data-tooltip="Disabled in demo"/)
  assert.doesNotMatch(out, / disabled=""/)
})

test('mode banner labels demo and live as drawn', () => {
  assert.match(html(<ModeBanner mode="demo" />), /DEMO<\/span><span>Fictional data · no real messages are sent · changes reset on reload/)
  assert.match(html(<ModeBanner mode="live" />), /LIVE<\/span><span>Connected to the real business · actions affect real customers/)
  assert.match(html(<ModeBanner mode="demo" compact />), /<span>Fictional data<\/span>/)
})

test('Sinhala and Tamil chips carry lang attributes for font selection', () => {
  assert.match(html(<LangChip lang="si" variant="native" />), /lang="si"[^>]*>සිංහල/)
  assert.match(html(<LangChip lang="ta" variant="native" />), /lang="ta"[^>]*>தமிழ்/)
  assert.match(html(<LangChip lang="en" />), />EN</)
})

test('inline error offers retry; chips render their text', () => {
  assert.match(html(<InlineError title="Could not load orders" body="Check your connection and try again." onRetry={() => undefined} />), /role="alert".*Retry/)
  assert.match(html(<Chip tone="violet">Negotiated</Chip>), /chip--violet">Negotiated/)
})

test('no fraud badge exists in the status vocabulary', () => {
  assert.doesNotMatch(html(<>{(['Confirmed', 'Processing', 'Dispatched', 'Delivered', 'Cancelled'] as const).map((s) => <StatusBadge key={s} status={s} />)}</>), /fraud/i)
})
