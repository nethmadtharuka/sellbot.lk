# SellBot.lk

> **Think [Daraz](https://www.daraz.lk) or [Kapruka](https://www.kapruka.com) -- but the entire shopping experience happens inside WhatsApp.**

In Sri Lanka, WhatsApp is how people communicate. SellBot.lk brings a furniture business directly into that conversation -- customers browse products with photos, place orders in natural language, negotiate prices, snap a photo of their bank slip for payment verification, receive a PDF invoice, and get delivery updates, all without leaving the chat.

The business owner manages everything through a React admin dashboard with real-time analytics and JWT-secured API access.

---

## How It Works

```
 Customer (WhatsApp)                          Business Owner
         |                                          |
         |  "Do you have chairs?"                   |
         v                                          |
 +---------------------------+             +---------------------+
 |      SellBotLk.Api        |             |  Admin Dashboard    |
 |                           |   REST API  |  (React + TS)       |
 |  WhatsApp Webhook      <--+------------>|                     |
 |  Gemini AI Engine         |   (JWT)     |  Analytics          |
 |  Order Pipeline           |             |  Orders             |
 |  Payment Matching         |             |  Delivery Zones     |
 |  Invoice Generation       |             +---------------------+
 +-------------+-------------+
               |
          MySQL (EF Core)
```

**A real customer journey:**

1. Customer sends *"chair ekak denna"* (Sinhala + English mix)
2. Gemini AI detects the language, classifies intent as product search, and returns matching products with prices
3. Bot sends **product images** alongside the text results -- each with a caption showing name and price
4. Customer replies *"I want 2 Oak Dining Chairs"* -- an order is created, stock is deducted, and **bank transfer instructions** are included in the confirmation
5. Bot sends an interactive WhatsApp message with **[Track Order]** and **[Cancel Order]** buttons
6. Customer photographs their bank transfer slip and sends it -- Gemini Vision reads the amount and matches it to the unpaid order, which goes to **Pending Review**
7. The owner checks the bank account and approves it on the dashboard -- the bot then confirms the payment and sends a **PDF invoice** as a WhatsApp document
8. Customer can later say *"order the same as last time"* to **reorder** their previous order instantly
9. Owner monitors analytics and updates delivery status from the admin dashboard -- customer receives WhatsApp notifications

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| Backend | ASP.NET Core 8, C# |
| AI | Google Gemini 2.5 Flash -- text intent parsing, product matching, multimodal vision |
| Messaging | Meta WhatsApp Cloud API -- inbound webhooks + outbound messages (text, images, documents, interactive buttons, typing indicator) |
| Database | MySQL 8 with Entity Framework Core (Pomelo) |
| PDF Generation | QuestPDF -- auto-generated invoices sent via WhatsApp |
| Frontend | React 19, TypeScript 6, Vite 8, Recharts |
| Auth | JWT Bearer tokens (admin API), HMAC-SHA256 (webhook verification) |
| Testing | xUnit, Moq, FluentAssertions |
| CI | GitHub Actions -- build, lint, test on every push |

---

## Features

### WhatsApp Bot

| Feature | How it works |
|---------|-------------|
| Natural language ordering | Customers type what they want in plain language; Gemini AI parses intent and creates orders against the real product catalogue |
| Tri-lingual support | Auto-detects and responds in English, Sinhala, or Tamil, including mixed-language messages |
| Product images | Search results include product photos sent as individual WhatsApp image messages with price captions |
| AI-powered product search | *"Do you have something modern and red?"* triggers a Gemini-backed search that returns the most relevant matches from the catalogue |
| Visual product search | Customer sends a furniture photo; Gemini Vision extracts attributes and the system matches them against catalogue products using weighted scoring |
| Price negotiation | Customers can haggle; the bot evaluates offers against minimum price rules and bulk discount tiers, then accepts, counters, or rejects |
| Payment flow | Order confirmations include bank transfer instructions; customer sends a bank slip; Gemini Vision matches it to an unpaid order and queues it for admin review. AI never marks an order Paid, and a slip or reference can only be used once |
| Unpaid order expiry | Unpaid orders are cancelled and their stock released after 24 hours (`Orders__UnpaidExpiryHours`, 0 to disable) |
| PDF invoices | After an admin approves the payment, a PDF invoice is generated with QuestPDF and sent as a WhatsApp document |
| Reorder | Returning customers can say *"order the same as last time"* to instantly re-create their most recent order at current prices |
| Interactive order buttons | Order confirmations include WhatsApp interactive buttons for **Track Order** and **Cancel Order** |
| Typing indicator | Bot shows a "typing..." indicator before every reply for a natural conversation feel |
| Delivery notifications | WhatsApp messages sent when an order moves to Processing, Dispatched, or Delivered |
| Document processing | Supplier invoices and payment slips processed through Gemini Vision for structured data extraction; supplier invoices restock inventory, and are only accepted from the owner's number (`OWNER_PHONE`) or the admin API |

### Admin Dashboard

| Page | Description |
|------|-------------|
| Analytics Dashboard | KPI cards (orders, revenue, paid/unpaid), revenue-over-time area chart, top products bar chart, orders-by-status pie chart, recent orders table; date range filtering (Recharts) |
| Orders | Filter by status and customer; color-coded badges for order and payment status |
| Order Details | Line items, totals, delivery info, fraud flags; review a payment slip and approve or reject it; actions to update status, set delivery with driver notes, or cancel (with automatic stock restoration) |
| Delivery Zones | 25 Sri Lankan zones with fees and ETAs; built-in serviceability checker for delivery cost and free-delivery eligibility |

### API

| Group | Endpoints |
|-------|-----------|
| Auth | `POST /login` -- returns JWT token |
| Analytics | `GET /analytics/summary` -- aggregated metrics with date range filtering |
| Products | CRUD, low-stock alerts, AI smart search, visual search |
| Orders | Create, list/filter, status transitions, delivery pipeline, cancellation with stock rollback, payment slip review (view, approve, reject) |
| Documents | Upload and process via Gemini Vision (invoices, payment slips, damage reports) |
| Delivery Zones | Zone listing and serviceability/fee calculation with Gemini fuzzy area-name matching |
| Webhook | Meta verification handshake + inbound message/event handler |
| Health | `GET /health` |

### Security

| Mechanism | Scope |
|-----------|-------|
| JWT Bearer authentication | All admin endpoints (`[Authorize]`) |
| Required admin credentials | No default login; the API won't start without a password of at least 12 characters |
| HMAC-SHA256 webhook verification | Every inbound POST verified against Meta's `X-Hub-Signature-256` |
| Rate limiting | Login: 5/minute per IP. Webhook: 20 messages/minute per sender, 600 signed requests/minute overall |
| Webhook idempotency | Each WhatsApp message ID is processed at most once, enforced by the database |
| Price and stock rules | The minimum price and available stock are enforced by the backend for every order; AI output is never trusted for either |
| Global error handling | Unhandled exceptions return RFC 9110 ProblemDetails JSON |
| Per-message error isolation | A failure processing one webhook message does not block others |
| Gemini retry with backoff | API calls retry up to 3 times with exponential backoff |

---

## Project Structure

```
SellBotLk/
├── SellBotLk.Api/
│   ├── Controllers/          Auth, Analytics, Products, Orders, Documents, DeliveryZones
│   ├── Webhooks/             WhatsApp inbound handler
│   ├── Services/             14 domain services (incl. InvoiceService, AnalyticsService)
│   ├── Integrations/         GeminiService, GeminiVisionService
│   ├── Middleware/            HMAC signature verification
│   ├── Models/               Entities (9 tables) + DTOs
│   └── Data/                 DbContext, repositories, EF migrations + seed data
├── sellbotlk-admin/
│   └── src/
│       ├── pages/            Dashboard, Orders, OrderDetails, DeliveryZones, Login
│       ├── components/       Reusable UI components, StatCard, layout shell
│       └── api/              Typed API client with JWT interceptor
├── SellBotLk.Tests/          Unit tests, plus end-to-end tests of money, stock and security rules on real MySQL
└── .github/workflows/        CI pipeline
```

---

## Getting Started

### Prerequisites

- .NET 8 SDK
- Node.js 20+
- MySQL 8
- [Meta WhatsApp Business](https://developers.facebook.com/docs/whatsapp/cloud-api/get-started) app
- [Google Gemini API key](https://ai.google.dev/)

### API

```bash
cp SellBotLk.Api/.env.example SellBotLk.Api/.env   # fill in your keys
cd SellBotLk.Api
dotnet ef database update                            # create/migrate the database
dotnet run                                           # http://localhost:5028 -- Swagger UI in dev
```

### Admin Dashboard

```bash
cd sellbotlk-admin
cp .env.example .env       # defaults to http://localhost:5028
npm install && npm run dev  # http://localhost:5173
```

Log in with the `ADMIN_USERNAME` and `ADMIN_PASSWORD` from your `.env`. There's no default password: the API won't start until you set one of at least 12 characters.

See [`.env.example`](SellBotLk.Api/.env.example) for the full list of required environment variables.

### Tests

The tests need a MySQL server, because they prove behaviour that depends on row locks and unique indexes. Each run creates its own `sellbot_test_*` database and drops it afterwards. For example, with Docker:

```bash
docker run -d --name sellbot-test-mysql -e MYSQL_ALLOW_EMPTY_PASSWORD=yes -p 33061:3306 mysql:8.4
SELLBOT_TEST_MYSQL="server=127.0.0.1;port=33061;user=root" dotnet test
```
