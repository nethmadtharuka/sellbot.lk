# Design parity checklist

Source of truth: `../design/*.png` (2x exports) with `SellBotLK Design Summary.pdf` as the page index
(29 PDF pages = the same 25 boards; p25 is blank, p26-29 split the two mobile boards; no PNG/PDF conflicts found).

**Verification method.** Each page was built, served (`vite preview`), screenshotted in headless Edge at the
board's viewport and 2x DPR, and diffed pixel-by-pixel against its PNG (mean absolute RGB difference, 0-255,
and % of pixels differing by more than 40). Mobile boards were rendered in a real 390 px iframe
(headless Edge enforces a ~500 px minimum window). Remaining differences are mostly text anti-aliasing,
the soft glows, and the items listed under "Known differences".

| Board (PNG) | Route | Viewport | Type | Status | Mean diff | px > 40 |
|---|---|---|---|---|---|---|
| Main | `/` | 1423 x 3347 * | page | verified | 11.4 | 4.8% |
| Login | `/login` | 1440 x 1024 | page | verified | 4.5 | 1.9% |
| MobileLogin | `/login` | 390 x 844 | page (mobile) | verified | 8.8 | 3.5% |
| Layouts | admin shell (all `/app/*`) | 1440 / 834 / 390 | shell spec sheet | built (shell); sheet itself not a route | n/a | n/a |
| States | `/states` | 1440 x 1069 | state sheet | verified | 10.2 | 4.5% |
| Overview | `/app` | 1423 x 1099 * | page | verified | 10.6 | 4.6% |
| TabletOverview | `/app` | 834 x 1194 | page (tablet) | verified | 9.6 | 2.8% |
| Orders | `/app/orders` | 1440 x 1024 | page | verified | 9.1 | 4.4% |
| MobileOrders | `/app/orders` | 390 x 844 | page (mobile) | verified | 17.0 | 6.0% |
| OrderDetail | `/app/orders/42` | 1440 x 1024 | page | verified | 8.5 | 3.8% |
| Customers | `/app/customers` | 1440 x 1024 | page | verified | 5.8 | 2.2% |
| CustomerDetail | `/app/customers/1` | 1440 x 1024 | page | verified | 5.4 | 2.0% |
| Inbox | `/app/inbox` | 1440 x 1024 | page | verified | 8.2 | 3.6% |
| Products | `/app/products` | 1440 x 1024 | page | verified | 5.9 | 2.3% |
| ProductDrawer | `/app/products/3` | 1440 x 1024 | drawer | verified | 8.2 | 3.0% |
| Payments | `/app/payments` | 1440 x 1024 | page | verified | 5.5 | 1.9% |
| Delivery | `/app/delivery` | 1440 x 1024 | page | verified | 4.5 | 1.5% |
| Analytics | `/app/analytics` | 1440 x 1024 | page | verified | 8.3 | 3.9% |
| Documents | `/app/documents` | 1440 x 1024 | page | verified | 7.9 | 3.0% |
| Integrations | `/app/integrations` | 1440 x 1024 | page | verified | 5.4 | 2.1% |
| Playground | `/app/playground` | 1440 x 1024 | page | verified | 6.6 | 2.7% |
| Journey | `/app/journey` | 1440 x 1024 | page | verified | 6.9 | 2.6% |
| Architecture | `/architecture` | 1440 x 1024 | page | verified | 8.0 | 3.3% |
| Api | `/api` | 1440 x 1024 | page | verified | 5.4 | 1.9% |
| Security | `/security` | 1440 x 1024 | page | verified | 5.7 | 2.3% |

\* Main and Overview are taller than the window, and their boards include a ~17 px scrollbar
(content ends 42 px from the right edge vs 24 px on the left), so they are compared at 1423 px.

## Components and states (States board)

| Item | Status |
|---|---|
| Loading skeleton | built, verified on States |
| Empty state with action | built, verified |
| Inline error with Retry | built, verified |
| Success toast | built, verified |
| Disabled controls (`aria-disabled` + tooltip explaining why) | built, verified |
| Confirm modal (focus trap, Esc, focus restore) | built, verified |
| DEMO and LIVE banners | built, verified |
| Status badges incl. dashed never-set Pending / Refunded | built, verified, unit-tested |
| Sinhala / Tamil / long-name rendering | built, verified (Noto Sans Sinhala/Tamil, `lang` attributes) |
| Drawer (Edit product) | built, verified on ProductDrawer |
| Pill tabs (arrow-key navigation), filter chips, search, table, pagination | built, verified on Orders |

## Responsive

| Width | Source | Status |
|---|---|---|
| >= 1200 | desktop boards | verified at 1280, 1366, 1440, 1920, 2560: shell spans the full window (see below); geometry at 1440 unchanged |
| 768-1199 | TabletOverview + Layouts (rail 72) | Overview verified; other pages **extrapolated** (rail, single-column grids) |
| < 768 | MobileOrders, MobileLogin + Layouts (drawer) | those two verified; all other pages **extrapolated** (top bar + drawer, stacked cards, scrollable tables) |
| 375 | brief requirement | all 21 routes: no horizontal page scroll (measured `scrollWidth`) |

## Known differences (honest list)

- Glows and gradients are approximations of the exported raster glows; they are the largest share of the remaining diff.
- MobileOrders board shows four order cards; the build shows the first page of seven (the list scrolls).
- Product drawer: the whole page behind the drawer is dimmed; the board keeps the sidebar at full brightness.
- Documents: the white pill under the invoice is blank in the board; it is reproduced blank with an accessible name and shows a "Simulated" toast.
- Overview "Revenue · last 14 days" chart is drawn as SVG (same line, fill and points); Recharts' responsive container logs console warnings, so it is no longer used.
- Layouts board is a specification sheet; it is implemented as the working shell rather than as its own route.
- "Back to site" (requested change): a link to the landing page (`/`) at the bottom of the desktop sidebar and of the mobile drawer; the mobile drawer brand and the tablet rail logo also link there (rail logo has a "Back to site" tooltip). Not drawn on the boards.
- Home navigation (requested change): the first nav item is labelled "Home" with a house icon; the boards label it "Overview". See below.

## Full-viewport fix and Home navigation (12 Nov 2026)

**Cause.** `.shell` in `src/styles/shell.css` had `max-width: 1440px; margin: 0 auto`, so on any window wider
than 1440 px the whole admin was boxed into a centred 1440 px column with empty bands on both sides.
Height was not limited (`min-height: 100vh`), but the sidebar and rail used `100vh`, which on mobile Chrome
includes the collapsing URL bar.

**Fix.** Removed the width cap and centring from `.shell`. The shell, background, sidebar and rail now use
`100dvh` with a `100vh` fallback. No other layout or styling changed.

**Home.** The first nav item is now **Home** (Lucide `house` icon) and opens the existing Overview page (`/app`).
It is the item previously labelled "Overview", relabelled rather than duplicated, so only one item highlights
on `/app`. It appears in the desktop sidebar, the tablet rail (icon with tooltip) and the mobile drawer; the
mobile top-bar title reads "Home" on that page. The Overview page itself is unchanged.

**Measured in headless Edge (same-origin iframe at each size):**

| Viewport | Navigation | Sidebar / rail / top bar | Main column | Page scroll width | Active item on /app |
|---|---|---|---|---|---|
| 2560 x 1440 | sidebar | x 16-280, y 16-1424 | x 304-2532 | 2560 (no overflow) | Home |
| 1920 x 1080 | sidebar | x 16-280, y 16-1064 | x 304-1892 | 1920 | Home |
| 1440 x 900 | sidebar | x 16-280, y 16-884 | x 304-1412 (as designed) | 1440 | Home |
| 1366 x 768 | sidebar | x 16-280, y 16-752 | x 304-1338 | 1366 | Home |
| 1280 x 800 | sidebar | x 16-280, y 16-784 | x 304-1252 | 1280 | Home |
| 834 x 1194 | rail | x 16-88, y 16-1178 | x 104-814 | 834 | Home |
| 390 x 844 | top bar + drawer | x 0-390, y 0-60 | x 16-374 | 390 | Home |
| 375 x 667 | top bar + drawer | x 0-375, y 0-60 | x 16-359 | 375 | Home |

On `/app/orders` and `/app/analytics` the active item is Orders / Analytics, and Home is not highlighted.
Overview re-diffed at the board size after the change: mean 10.5, 4.7% of pixels over 40 (before: 10.6 / 4.6%).
Checks: `tsc -b` passes, `eslint` clean, `npm test` 14/14, `vite build` passes.

## Landing and presentation pages full width (12 Nov 2026)

**Cause.** `.landing > section` had `max-width: 1392px; margin: 0 auto`, and the presentation pages capped
`.sc-nav` / `.sc-main` at 1344 px, so on windows wider than the 1440 board the cards were centred with empty
space at both sides.

**Fix.** Removed those caps (the 24 px landing margin and 48 px presentation padding from the boards stay).
Because the hero card now widens, the three floating chips are anchored to the card centre using the offsets
measured on the 1375 px design card, and the green arc scales with the card (`max(1552px, 113%)`) so its
sides still meet the card edges. Headline, wordmark and section type keep their designed sizes.

**Verified.** Landing re-diffed at the board size: mean 11.38, 4.8% (unchanged). Screenshots at 1838 px show
edge-to-edge cards; all 21 routes at 1838 and 2560 px have no horizontal overflow (the arc is clipped inside the
hero card). Checks: `tsc -b`, `eslint`, `npm test` 14/14 and `vite build` pass.

## Scroll motion layer (13 Nov 2026)

GSAP 3.15.1 (+ ScrollTrigger) and Lenis 1.3.27, on the landing page and the Architecture / API / Security
pages only (the admin console keeps native scrolling and never loads the motion chunk). All values live in
`src/motion/motionConfig.ts`.

| Check | Result |
|---|---|
| `?motion=off` (or `motionConfig.enabled = false`) vs Main board | mean 11.38, 4.8% — identical to before the motion work |
| Reduced motion (forced in Edge) | full page visible in final state; ticker shows a static first word |
| Scroll sequence 1440 x 900 (0 / 300 / 900 / 1300 / 1800 / 2300 / 3050 / end) | hero as designed at 0 (tilt and ghost layers removed on request, 13 Nov; top-of-page diff vs board 7.5); expanding to full width with copy exit; how sheet over pinned hero; cards converge while pinned; composed cards + ring; ticker + icon; built sheet over dimmed pick |
| Horizontal overflow, motion on, 21 routes at 375 and 1440 px | none |
| Console errors/warnings, motion on, same routes | none |
| Not verified | 4x CPU-throttled frame timing (no DevTools performance panel in headless); hand-scrolling feel; real touch devices |

Skipped because SellBotLK has no matching element: 3.4 (phone screen swap; the hero phone was removed by design),
3.11 (segmented tab scenes; no single-step-at-a-time content), the accordion half of 3.13, and the thumbnails/tags
half of 3.10. The hero nav is not sticky in the design, so 3.6 restyles it only at the hero expansion threshold.
