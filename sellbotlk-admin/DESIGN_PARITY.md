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
| >= 1200 | desktop boards | verified at 1280, 1440, 1920 (shell capped at 1440 and centred; no overflow) |
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
