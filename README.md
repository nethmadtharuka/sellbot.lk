# SellBot.lk

> **Think Daraz or Kapruka — but the entire shopping experience happens inside WhatsApp.**

Sri Lankan customers already live on WhatsApp. Instead of forcing them onto a website, SellBot.lk lets a furniture business sell directly through chat. Customers browse products, place orders, haggle on prices, snap a photo of a bank slip, and track delivery — all without leaving WhatsApp. The business owner manages everything from a React admin dashboard.

Built as a full-stack capstone project demonstrating AI integration, webhook-driven architecture, and multi-language support.

---

## How It Works

```
Customer (WhatsApp)                         Business Owner
        │                                          │
        │  "Do you have chairs?"                   │
        ▼                                          │
┌──────────────────────┐                  ┌────────────────────┐
│   SellBotLk.Api      │                  │  Admin Dashboard   │
│                      │     REST API     │  (React + TS)      │
│  WhatsApp Webhook ◄──┼──────────────────┤                    │
│  Gemini AI Engine    │                  │  Orders / Delivery │
│  Order Pipeline      │                  │  Zone Management   │
│  Payment Matching    │                  └────────────────────┘
│  Delivery Tracking   │
└──────────┬───────────┘
           │
      MySQL (EF Core)
```

**A typical customer journey:**

1. Customer sends *"මට chairs තියනවද?"* (Sinhala + English mix)
2. Gemini detects language and intent → returns matching products with prices
3. Customer replies *"I want 2 Oak Dining Chairs"* → order created, stock deducted
4. Customer sends a bank transfer slip photo → Gemini Vision extracts amount, auto-matches to order
5. Owner confirms from admin dashboard → customer gets WhatsApp delivery updates

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core 8, C# |
| AI | Google Gemini 2.5 Flash (text parsing + vision) |
| Messaging | Meta WhatsApp Cloud API (webhooks + Graph API) |
| Database | MySQL 8 + Entity Framework Core (Pomelo) |
| Frontend | React 19 + TypeScript + Vite 8 |
| CI/CD | GitHub Actions |
| Testing | xUnit + Moq + FluentAssertions |

---

## Features

### WhatsApp Bot

| Feature | Description |
|---------|-------------|
| **Conversational commerce** | Natural language ordering — *"I want 2 chairs and a table"* creates an order |
| **Multi-language** | Auto-detects English, Sinhala, and Tamil — replies in the customer's language |
| **AI product search** | *"Do you have something modern and red?"* → Gemini matches against real catalogue |
| **Visual search** | Customer sends a photo of furniture → Gemini Vision extracts attributes → finds similar products |
| **Price negotiation** | Customers can haggle — bot checks against minimum price and bulk discount rules |
| **Payment verification** | Customer sends bank slip photo → Gemini Vision extracts amount → auto-matches to unpaid order |
| **Delivery notifications** | Status changes (dispatched, delivered) trigger WhatsApp messages to customer |
| **Document processing** | PDF invoices and receipts parsed via Gemini Vision for data extraction |

### Admin Dashboard

| Page | What it does |
|------|-------------|
| **Orders** | Filter by status/customer, view order list with payment and status badges |
| **Order Details** | Line items, totals, fraud flags; update status, delivery status + driver notes, cancel |
| **Delivery Zones** | Manage zones with fees, ETAs, free-delivery thresholds; check serviceability |

### API

| Area | Endpoints |
|------|-----------|
| Products | CRUD, low-stock alerts, AI smart search, visual search |
| Orders | Create, filter, status updates, delivery pipeline, cancellation with stock restore |
| Documents | Upload and process invoices/slips with Gemini Vision |
| Delivery Zones | Zone listing, serviceability + fee calculator |
| Webhook | WhatsApp verification + inbound message handler |
| Health | `/health` endpoint |

### Security & Reliability

- **HMAC-SHA256** verification on all inbound webhooks (Meta `X-Hub-Signature-256`)
- **Global error handling** with RFC 9110 ProblemDetails responses
- **Per-message error isolation** — one bad message doesn't kill the batch
- **Gemini retry with exponential backoff** — 3 attempts before fallback
- **Always returns 200 to Meta** — prevents webhook retry storms

---

## Project Structure

```
SellBotLk/
├── SellBotLk.Api/                 # ASP.NET Core backend
│   ├── Controllers/               # REST endpoints (Products, Orders, Documents, DeliveryZones)
│   ├── Webhooks/                  # WhatsApp webhook handler
│   ├── Services/                  # Business logic (11 services)
│   ├── Integrations/Gemini/       # Gemini AI text + vision
│   ├── Middleware/                # HMAC verification
│   ├── Models/                    # Entities + DTOs
│   └── Data/                      # EF Core DbContext, repositories, migrations
├── sellbotlk-admin/               # React admin dashboard
│   └── src/
│       ├── pages/                 # Orders, OrderDetails, DeliveryZones
│       ├── components/            # UI kit, layout
│       └── api/                   # API client
├── SellBotLk.Tests/               # Unit tests (17 tests)
└── .github/workflows/ci.yml       # CI pipeline
```

---

## Getting Started

### Prerequisites

- .NET 8 SDK
- Node.js 20+
- MySQL 8
- A [Meta WhatsApp Business](https://developers.facebook.com/docs/whatsapp/cloud-api/get-started) app (for webhook + messaging)
- A [Google Gemini API key](https://ai.google.dev/)

### 1) API

```bash
# Copy environment template and fill in your keys
cp SellBotLk.Api/.env.example SellBotLk.Api/.env

# Or configure appsettings.Development.json (gitignored)

cd SellBotLk.Api
dotnet run
```

API runs at `http://localhost:5028` — Swagger UI available in Development.

### 2) Admin Dashboard

```bash
cd sellbotlk-admin
cp .env.example .env          # default points to localhost:5028
npm install
npm run dev
```

Dashboard runs at `http://localhost:5173`.

### Environment Variables

<details>
<summary>API (.env.example)</summary>

| Variable | Description |
|----------|-------------|
| `GEMINI_API_KEY` | Google Gemini API key |
| `WHATSAPP_TOKEN` | Meta WhatsApp permanent token |
| `WHATSAPP_VERIFY_TOKEN` | Custom webhook verification token |
| `WHATSAPP_PHONE_NUMBER_ID` | WhatsApp business phone number ID |
| `DB_CONNECTION_STRING` | MySQL connection string |
| `JWT_SECRET` | JWT signing key (32+ chars) |
| `OWNER_PHONE` | Business owner's WhatsApp number |

</details>

<details>
<summary>Admin (.env.example)</summary>

| Variable | Default | Description |
|----------|---------|-------------|
| `VITE_API_BASE_URL` | `http://localhost:5028` | API base URL |

</details>

---

## CI

GitHub Actions runs on every push and PR:

| Job | Steps |
|-----|-------|
| **Admin** | `npm ci` → `npm run build` → `npm run lint` |
| **API** | `dotnet restore` → `dotnet build` → `dotnet test` (17 tests) |

---

## Database Schema

9 tables managed via EF Core migrations:

| Table | Purpose |
|-------|---------|
| `Customers` | Phone, name, language preference, order history |
| `Products` | Catalogue with pricing, stock, bulk discounts, attributes |
| `Orders` | Order lifecycle, payment status, fraud flags |
| `OrderItems` | Line items with negotiated pricing |
| `Conversations` | Per-customer chat state for context-aware AI |
| `Documents` | Processed invoices and payment slips |
| `DeliveryZones` | Areas with fees, ETAs, free-delivery thresholds |
| `InventoryLogs` | Stock change audit trail |
| `DailyReports` | Business metrics snapshots |
