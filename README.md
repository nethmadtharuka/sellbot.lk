# SellBot.lk

> **Think [Daraz](https://www.daraz.lk) or [Kapruka](https://www.kapruka.com) — but the entire shopping experience happens inside WhatsApp.**

In Sri Lanka, WhatsApp is how people communicate. SellBot.lk brings a furniture business directly into that conversation — customers browse products, place orders in natural language, negotiate prices, snap a photo of their bank slip for payment verification, and receive delivery updates, all without leaving the chat.

The business owner manages everything through a React admin dashboard.

---

## How It Works

```
 Customer (WhatsApp)                          Business Owner
         │                                          │
         │  "Do you have chairs?"                   │
         ▼                                          │
 ┌───────────────────────┐                 ┌────────────────────┐
 │    SellBotLk.Api      │                 │  Admin Dashboard   │
 │                       │    REST API     │  (React + TS)      │
 │  WhatsApp Webhook  ◄──┼────────────────►│                    │
 │  Gemini AI Engine     │                 │  Orders            │
 │  Order Pipeline       │                 │  Delivery Zones    │
 │  Payment Matching     │                 └────────────────────┘
 │  Delivery Tracking    │
 └───────────┬───────────┘
             │
        MySQL (EF Core)
```

**A real customer journey:**

1. Customer sends *"මට chairs තියනවද?"* (Sinhala + English mix)
2. Gemini AI detects the language, classifies intent as product search, and returns matching products with prices
3. Customer replies *"I want 2 Oak Dining Chairs"* — an order is created and stock is deducted
4. Customer photographs their bank transfer slip and sends it — Gemini Vision extracts the amount and auto-matches it to the unpaid order
5. Owner updates status from the admin dashboard — customer receives a WhatsApp delivery notification

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core 8, C# |
| AI | Google Gemini 2.5 Flash — text intent parsing, product matching, and multimodal vision |
| Messaging | Meta WhatsApp Cloud API — inbound webhooks + outbound messages via Graph API |
| Database | MySQL 8 with Entity Framework Core (Pomelo provider) |
| Frontend | React 19, TypeScript, Vite 8 |
| Testing | xUnit, Moq, FluentAssertions (17 tests) |
| CI | GitHub Actions — build, lint, and test on every push |

---

## Features

### Conversational Commerce (WhatsApp Bot)

- **Natural language ordering** — Customers type what they want in plain language; Gemini AI parses intent and creates orders against the real product catalogue
- **Tri-lingual support** — Auto-detects and responds in English, Sinhala, or Tamil, including mixed-language messages like *"chair ekak denna"*
- **AI-powered product search** — *"Do you have something modern and red?"* triggers a Gemini-backed search that scores and returns the top matches from the catalogue
- **Visual product search** — Customer sends a furniture photo; Gemini Vision extracts attributes (type, color, material, style) and the system matches them against catalogue products using weighted scoring
- **Price negotiation** — Customers can haggle; the bot evaluates offers against minimum price rules and bulk discount tiers, then accepts, counters, or rejects
- **Payment slip verification** — Customer sends a bank slip photo; Gemini Vision extracts the transfer amount and reference; the system auto-matches it to the correct unpaid order within a configurable tolerance
- **Delivery status notifications** — WhatsApp messages are sent to the customer when their order moves to Processing, Dispatched, or Delivered
- **Document processing** — Supplier invoices and payment slips are processed through Gemini Vision for structured data extraction; supplier invoices trigger automatic inventory restocking

### Admin Dashboard (React)

- **Orders list** — Filter by status and customer ID; color-coded badges for order and payment status
- **Order details** — Full breakdown with line items, totals, delivery info, and fraud flags; actions to update status, set delivery status with driver notes, or cancel (with automatic stock restoration)
- **Delivery zones** — View all 25 seeded Sri Lankan zones with fees and ETAs; built-in serviceability checker that calculates delivery cost and free-delivery eligibility for a given area and order total

### API (25 endpoints)

- **Products** — Full CRUD, low-stock alerts, AI-powered smart search, multimodal visual search
- **Orders** — Create, list/filter, status transitions, delivery pipeline updates, cancellation with stock rollback
- **Documents** — Upload and process via Gemini Vision (invoices, payment slips, damage reports)
- **Delivery Zones** — Zone listing and serviceability/fee calculation with Gemini fuzzy area-name matching
- **WhatsApp Webhook** — Meta verification handshake + inbound message/event handler
- **Health** — `GET /health`

### Security and Reliability

- **HMAC-SHA256 webhook verification** — Every inbound `POST` is verified against Meta's `X-Hub-Signature-256` header before reaching the controller
- **Global error handling** — Unhandled exceptions return structured RFC 9110 ProblemDetails JSON instead of leaking stack traces
- **Per-message error isolation** — A failure processing one message in a webhook batch does not prevent others from being processed
- **Gemini retry with backoff** — API calls retry up to 3 times with exponential backoff before falling back to a safe default response
- **Always returns 200 to Meta** — Prevents Meta from disabling the webhook due to repeated error responses

---

## Project Structure

```
SellBotLk/
├── SellBotLk.Api/
│   ├── Controllers/          Products, Orders, Documents, DeliveryZones
│   ├── Webhooks/             WhatsApp inbound handler
│   ├── Services/             11 domain services
│   ├── Integrations/         Gemini text + Gemini Vision (2 clients)
│   ├── Middleware/            HMAC signature verification
│   ├── Models/               Entities (9 tables) + DTOs
│   └── Data/                 DbContext, repositories, EF migrations + seed data
├── sellbotlk-admin/
│   └── src/
│       ├── pages/            Orders, OrderDetails, DeliveryZones
│       ├── components/       Reusable UI components + layout shell
│       └── api/              Typed API client
├── SellBotLk.Tests/          17 unit tests
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
dotnet run                                           # http://localhost:5028 — Swagger in dev
```

### Admin Dashboard

```bash
cd sellbotlk-admin
cp .env.example .env       # defaults to http://localhost:5028
npm install && npm run dev  # http://localhost:5173
```

See [`.env.example`](SellBotLk.Api/.env.example) for the full list of required environment variables.
