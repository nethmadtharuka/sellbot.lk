# SellBotLK — Figma Page Checklist

Companion to `SELLBOTLK_SHOWCASE_ROADMAP.md`. Page IDs match the roadmap (A = admin layout, R = presentation layout). Visual direction (colours, fonts, theme) is yours to choose from Pinterest. This list covers structure, not style.

**Breakpoints for every page:** Desktop **1440×1024** · Tablet **834×1194** · Mobile **390×844**
**Mode banner on every admin frame:** design both `DEMO · fictional data` and `LIVE · actions affect real customers` variants.

---

## 0. Before any page (Phase 1)

- [ ] Figma file pages: Cover · Foundations · Components · Templates · Pages (Phase 1–5) · Prototype · Handoff
- [ ] Colour **roles** (not values yet): background, surface, text, muted, border, brand, success, warning, danger, info
- [ ] Status roles: OrderStatus ×6 (Pending*, Confirmed, Processing, Dispatched, Delivered, Cancelled), PaymentStatus ×4 (Unpaid, PendingReview, Paid, Refunded*), ConversationState ×6, Language ×3 (en/si/ta), Intent ×10. *Never set by current code, but design them anyway.
- [ ] Type scale + a font that renders **Sinhala and Tamil** (test: ආයුබෝවන් · வணக்கம்)
- [ ] Spacing (4/8 grid), radius, elevation, grid per breakpoint
- [ ] Number format `LKR 28,500`, date `10 Nov 2026`, masked phone `+94 70 ••• ••01`
- [ ] Templates: **AdminLayout** (sidebar 240–280 px → icon rail on tablet → drawer on mobile) and **PresentationLayout** (top nav + reading column)

### Shared components (build once, with variants and states)

| Group | Components |
|---|---|
| Shell | Sidebar (groups, badges, active, collapsed), Topbar, ModeBanner, PageHeader, Breadcrumb |
| Inputs | Button (primary/secondary/danger/ghost × default/hover/focus/disabled/loading), IconButton, Input, Select, SearchInput, TextArea, Field (label/hint/error), FilterChips, Tabs, DateRangeBar, Dropzone |
| Display | Card, StatCard, ChartCard, DataTable (header, sort, row hover, row actions, pagination, mobile card mode), Badge (OrderStatus, PaymentStatus, ConversationState), LanguageChip, IntentChip, ConfidenceMeter, CountdownChip, StockMeter, PriceLadder, MaskedPhone, KeyValueList, Timeline, StatusStepper, JsonViewer, CodeBlock |
| Feedback | Toast (success/error/"Simulated"), InlineAlert, EmptyState, Skeleton, ConfirmDialog ("This sends a WhatsApp message" variant), Drawer, Modal |
| Chat | ConversationListItem, MessageBubble (in/out × text/image/document/interactive × annotated), SystemEventRow, TypingIndicator, PhoneFrame |
| Showcase | Hero, EntryTile, TechBadge, StepStrip, PipelineStep, DiagramHotspot, GuaranteeCard, EndpointRow, MethodBadge |

### Universal UI states (design wherever they apply)

- [ ] Loading (skeleton) · [ ] Empty · [ ] No results for filter · [ ] Error + retry · [ ] Session expired (Live) · [ ] Long text and long names · [ ] Demo vs Live banner

---

## Phase 1 — Foundation

### A1 Login · Standalone · Live only
- **Pinterest:** "minimal SaaS login page", "admin sign in split screen", "B2B login card"
- **Layout:** centred card · logo · username · password (show/hide) · submit · error area · "Live environment" note
- **Frames:** D · T · M
- **States:** [ ] default [ ] invalid credentials [ ] rate-limited (too many attempts) [ ] loading
- **Responsive:** card full-width on mobile; no sidebar

### Shell templates
- [ ] AdminLayout D / T / M (sidebar expanded / rail / drawer open + closed)
- [ ] PresentationLayout D / T / M (top nav, mobile menu)
- [ ] Reference test page: A5 Orders built from components only

---

## Phase 2 — Core Business Administration

### A2 Overview · Admin · Live + Demo
- **Pinterest:** "e-commerce admin dashboard overview", "operations dashboard needs attention", "KPI cards dashboard minimal"
- **Layout:** KPI row (4) · "Needs action" (payments to review, unpaid orders expiring, low stock) · recent orders · mini revenue chart
- **Components:** StatCard, ActionQueueCard, DataTable (compact), MiniAreaChart, Badges
- **Frames:** D · T · M (KPIs 2×2, queues stacked)
- **States:** [ ] loading [ ] new business (all zero) [ ] normal [ ] all caught up

### A3 Products · Admin · Live + Demo
- **Pinterest:** "inventory management table thumbnails", "product catalog admin grid list toggle", "stock level indicator UI"
- **Layout:** toolbar (search, category, stock filter, Add) · table/grid toggle · row → drawer
- **Components:** DataTable, ProductThumb, StockMeter, ViewToggle, FilterChips
- **Frames:** D · T · M (card list)
- **States:** [ ] loading [ ] empty catalogue [ ] no results [ ] low-stock rows [ ] out of stock

### A4 Product detail (drawer) · Admin · Live + Demo
- **Pinterest:** "product edit side drawer", "pricing tiers card UI", "inventory history timeline"
- **Layout:** drawer with tabs: Details · Pricing rules (list → bulk → floor "lowest the bot accepts") · Inventory history
- **Frames:** D (drawer over A3) · M (full page)
- **States:** [ ] view [ ] edit [ ] validation error (MinPrice > Price) [ ] saved [ ] deactivate confirm

### A5 Orders · Admin · Live + Demo
- **Pinterest:** "order management dashboard filters", "orders table status tabs", "e-commerce orders admin badges"
- **Layout:** status tabs · payment chips (Unpaid / Pending review / Paid) · search · table (number, customer, items, total, order badge, payment badge, created, expiry countdown)
- **Frames:** D · T · M (cards)
- **States:** [ ] loading [ ] empty [ ] filtered empty [ ] pagination [ ] negotiated-price marker

### A6 Order detail · Admin · Live + Demo
- **Pinterest:** "order details page admin", "order timeline stepper", "payment verification panel"
- **Layout:** header (number + 2 badges + actions) · left: items, totals, customer · right: payment card (slip, approve/reject), fulfilment stepper, activity timeline
- **Frames:** D · T · M (single column, actions in a sticky bottom bar)
- **States:** [ ] Unpaid [ ] PendingReview [ ] Paid [ ] Dispatched [ ] Delivered [ ] Cancelled [ ] action error [ ] confirm dialog ("customer will be notified")
- **Note:** no fraud badge; only valid next fulfilment steps offered

### A7 Customers · Admin · Demo (Live after backend B1)
- **Pinterest:** "CRM customer list table", "customer directory dashboard"
- **Layout:** search + language filter · table (name, masked phone, language, orders, spend, last order)
- **Frames:** D · T · M
- **States:** [ ] loading [ ] empty [ ] Live "endpoint planned" notice

### A8 Customer detail · Admin · Demo (optional)
- **Pinterest:** "CRM customer profile page", "customer 360 view"
- **Layout:** profile header · KPIs · orders table · conversation state card · "open thread" link
- **Frames:** D · M
- **States:** [ ] no orders [ ] many orders

---

## Phase 3 — WhatsApp Inbox and AI Experience

### A9 WhatsApp Inbox · Admin · Demo (Live limited)
- **Pinterest:** "WhatsApp business inbox dashboard", "customer support shared inbox three panel", "chat inbox conversation list UI", "chatbot conversation annotations"
- **Layout:** list (search, language + state filters) · thread (bubbles, chips: language / intent / confidence / template-or-AI, system events like "Order created · stock −1") · context panel (customer, state, negotiation card, open order, AI trace)
- **Frames:** D (3 panes) · T (2 panes + context drawer) · M (list → thread)
- **States:** [ ] no conversation selected [ ] typing [ ] image message [ ] payment slip [ ] document [ ] interactive buttons shown **disabled** ("reply handling not implemented") [ ] AI fallback reply [ ] long Sinhala/Tamil text [ ] "English template in current build" note [ ] simulation banner
- **Do not:** copy WhatsApp's logo, colours or exact UI

### A10 AI Playground · Admin · Live + Demo
- **Pinterest:** "AI playground interface", "LLM prompt testing console", "image analysis result UI"
- **Layout:** cards: Understand a message · Find products · Search by photo · Delivery area · Negotiation rules. Each card has input → result (chips, ranked list, JSON viewer, WhatsApp preview bubble).
- **Frames:** D · T · M
- **States:** [ ] idle [ ] loading (several seconds) [ ] result [ ] AI unavailable [ ] "Live call to Gemini, nothing sent to WhatsApp" notice

### R2 Customer Journey Player · Presentation · Demo
- **Pinterest:** "chatbot flow visualization", "phone mockup chat with explanation", "process step animation web"
- **Layout:** scenario tabs (Sinhala order · Tamil support · English negotiation · Photo search) · phone chat (left) · pipeline steps with file/method + data + status chip (right) · playback controls
- **Frames:** D · T · M (phone above steps)
- **States:** [ ] step pending / active / done / partial [ ] reduced-motion variant

---

## Phase 4 — Business Workflows and Analytics

### A11 Payment review · Admin · Live + Demo
- **Pinterest:** "payment verification dashboard", "receipt review approve reject", "document review side by side"
- **Layout:** queue (oldest first) · slip viewer (zoom/rotate) · extracted vs expected comparison (amount, reference) · approve / reject with reason
- **Frames:** D · T · M (queue → review screen)
- **States:** [ ] empty queue ("all caught up") [ ] image slip [ ] PDF slip [ ] amount match [ ] reject dialog [ ] approved toast (Live: "confirmation + invoice sent")
- **Demo:** slip artwork watermarked "SAMPLE — NOT A REAL TRANSFER"

### A12 Delivery · Admin · Live + Demo
- **Pinterest:** "delivery management kanban", "logistics order tracking dashboard", "delivery zones pricing table"
- **Layout:** Tab Board (Confirmed · Processing · Dispatched · Delivered) · Tab Zones (25 zones table + fee checker)
- **Frames:** D · T (horizontal scroll) · M (status tabs instead of columns)
- **States:** [ ] empty column [ ] card with no delivery area [ ] advance dialog (driver note, "customer will be notified") [ ] checker: serviceable / free delivery / not serviceable

### A13 Analytics · Admin · Live + Demo
- **Pinterest:** "sales analytics dashboard date range", "revenue chart dashboard card"
- **Layout:** date range · KPIs · revenue area chart · top products bar · status donut · recent orders
- **Frames:** D · T · M
- **States:** [ ] zero data [ ] single day [ ] 90 days

### A14 Documents & OCR · Admin · Live + Demo (optional)
- **Pinterest:** "OCR invoice extraction UI", "file upload dropzone results table"
- **Layout:** upload (type: Supplier invoice / Damage report) · documents table · detail drawer (extracted line items, notes)
- **Frames:** D · M
- **States:** [ ] uploading [ ] extracted [ ] partial match ("2 products not found") [ ] failed [ ] "this changes stock" confirm

### A15 Integrations & status · Admin · Demo (Live after backend B6, optional)
- **Pinterest:** "integrations page SaaS cards", "system status page"
- **Layout:** cards: WhatsApp Cloud API · Gemini · MySQL · Maintenance job. Each card has a status dot, its purpose and key facts. **No secret values.**
- **Frames:** D · M
- **States:** [ ] configured [ ] not configured [ ] unreachable

---

## Phase 5 — Recruiter Showcase and Polish

### R1 Recruiter landing · Presentation · Demo
- **Pinterest:** "SaaS landing page hero dashboard mockup", "developer case study page", "AI product landing page"
- **Layout:** hero (pitch + Enter demo + GitHub) · 3-step strip · 3 entry tiles (Admin · WhatsApp AI · Engineering) · stack badges · "fictional data" note
- **Frames:** D · T · M + Open Graph image 1200×630
- **States:** [ ] default [ ] focus states on CTAs

### R3 Architecture & stack · Presentation · Static
- **Pinterest:** "system architecture diagram website", "technical case study design", "software architecture infographic"
- **Layout:** sticky section nav · system diagram with hotspots · request pipeline · data model · state machines · stack table · design decisions
- **Frames:** D · T · M (diagram scrolls or simplifies)
- **States:** [ ] hotspot selected

### R4 API reference · Presentation · Static (optional)
- **Pinterest:** "API documentation website design", "developer docs endpoint list"
- **Layout:** grouped endpoints · method + auth badges · fictional request/response code blocks
- **Frames:** D · M
- **States:** [ ] endpoint collapsed / expanded [ ] copy confirmation

### R5 Security, reliability & testing · Presentation · Static
- **Pinterest:** "security trust page SaaS", "test coverage report UI", "layered defense diagram"
- **Layout:** guarantee → mechanism → test cards · webhook defence layers diagram · test suite table · known limitations
- **Frames:** D · T · M

---

## Handoff checklist (every frame)

- [ ] Route, layout and phase noted on the frame
- [ ] Data source noted (`data.listOrders`, …) and Live/Demo availability
- [ ] All applicable states present
- [ ] Desktop, tablet and mobile frames present
- [ ] Interactions prototyped (navigation, drawer, dialog, filter, step advance)
- [ ] Components used from the library (no detached instances)
- [ ] Contrast checked (WCAG AA), focus order annotated, icons paired with text for status
- [ ] Sinhala/Tamil sample text checked in the chosen font
- [ ] No real customer data, phone numbers, slips or bank details in any frame

## Do not design

Storefront/cart/checkout · customer login · Settings with secrets · user/role management · live agent reply box · WhatsApp template manager · fraud center · daily reports · delivery-zone editor · jobs dashboard · voice-note feature
