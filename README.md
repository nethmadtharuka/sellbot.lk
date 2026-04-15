# SellBot.lk

> **Think [Daraz](https://www.daraz.lk) or [Kapruka](https://www.kapruka.com) -- but the entire shopping experience happens inside WhatsApp.**

In Sri Lanka, WhatsApp is how people communicate. SellBot.lk brings a furniture business directly into that conversation -- customers browse products, place orders in natural language, negotiate prices, snap a photo of their bank slip for payment verification, and receive delivery updates, all without leaving the chat.

The business owner manages everything through a React admin dashboard with JWT-secured API access.

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
 |  Gemini AI Engine         |   (JWT)     |  Login              |
 |  Order Pipeline           |             |  Orders             |
 |  Payment Matching         |             |  Delivery Zones     |
 |  Delivery Tracking        |             +---------------------+
 +-------------+-------------+
               |
          MySQL (EF Core)
```

**A real customer journey:**

1. Customer sends *"chair ekak denna"* (Sinhala + English mix)
2. Gemini AI detects the language, classifies intent as product search, and returns matching products with prices
3. Customer replies *"I want 2 Oak Dining Chairs"* -- an order is created and stock is deducted
4. Bot sends an interactive WhatsApp message with **[Track Order]** and **[Cancel Order]** buttons
5. Customer photographs their bank transfer slip and sends it -- Gemini Vision extracts the amount and auto-matches it to the unpaid order
6. Owner updates status from the admin dashboard -- customer receives a WhatsApp delivery notification

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| Backend | ASP.NET Core 8, C# |
| AI | Google Gemini 2.5 Flash -- text intent parsing, product matching, multimodal vision |
| Messaging | Meta WhatsApp Cloud API -- inbound webhooks + outbound messages (text, interactive buttons, typing indicator) |
| Database | MySQL 8 with Entity Framework Core (Pomelo) |
| Frontend | React 19, TypeScript 6, Vite 8 |
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
| AI-powered product search | *"Do you have something modern and red?"* triggers a Gemini-backed search that returns the most relevant matches from the catalogue |
| Visual product search | Customer sends a furniture photo; Gemini Vision extracts attributes and the system matches them against catalogue products using weighted scoring |
| Price negotiation | Customers can haggle; the bot evaluates offers against minimum price rules and bulk discount tiers, then accepts, counters, or rejects |
| Payment slip verification | Customer sends a bank slip photo; Gemini Vision extracts the transfer amount and reference; the system auto-matches it to the correct unpaid order |
| Interactive order buttons | Order confirmations include WhatsApp interactive buttons for **Track Order** and **Cancel Order** |
| Typing indicator | Bot shows a "typing..." indicator before every reply for a natural conversation feel |
| Delivery notifications | WhatsApp messages sent when an order moves to Processing, Dispatched, or Delivered |
| Document processing | Supplier invoices and payment slips processed through Gemini Vision for structured data extraction; supplier invoices trigger automatic inventory restocking |

### Admin Dashboard

| Page | Description |
|------|-------------|
| Login | JWT-authenticated login gate; tokens stored in browser, auto-redirect on 401 |
| Orders | Filter by status and customer; color-coded badges for order and payment status |
| Order Details | Line items, totals, delivery info, fraud flags; actions to update status, set delivery with driver notes, or cancel (with automatic stock restoration) |
| Delivery Zones | 25 Sri Lankan zones with fees and ETAs; built-in serviceability checker for delivery cost and free-delivery eligibility |

### API -- 24 Endpoints

| Group | Endpoints |
|-------|-----------|
| Auth | `POST /login` -- returns JWT token |
| Products | CRUD, low-stock alerts, AI smart search, visual search |
| Orders | Create, list/filter, status transitions, delivery pipeline, cancellation with stock rollback |
| Documents | Upload and process via Gemini Vision (invoices, payment slips, damage reports) |
| Delivery Zones | Zone listing and serviceability/fee calculation with Gemini fuzzy area-name matching |
| Webhook | Meta verification handshake + inbound message/event handler |
| Health | `GET /health` |

### Security

| Mechanism | Scope |
|-----------|-------|
| JWT Bearer authentication | All admin endpoints (`[Authorize]`) |
| HMAC-SHA256 webhook verification | Every inbound POST verified against Meta's `X-Hub-Signature-256` |
| Rate limiting | Webhook endpoint -- 60 requests/minute per IP (fixed window) |
| Global error handling | Unhandled exceptions return RFC 9110 ProblemDetails JSON |
| Per-message error isolation | A failure processing one webhook message does not block others |
| Gemini retry with backoff | API calls retry up to 3 times with exponential backoff |

---

## Project Structure

```
SellBotLk/
├── SellBotLk.Api/
│   ├── Controllers/          Auth, Products, Orders, Documents, DeliveryZones
│   ├── Webhooks/             WhatsApp inbound handler
│   ├── Services/             11 domain services
│   ├── Integrations/         GeminiService, GeminiVisionService
│   ├── Middleware/            HMAC signature verification
│   ├── Models/               Entities (9 tables) + DTOs
│   └── Data/                 DbContext, repositories, EF migrations + seed data
├── sellbotlk-admin/
│   └── src/
│       ├── pages/            Login, Orders, OrderDetails, DeliveryZones
│       ├── components/       Reusable UI components + layout shell
│       └── api/              Typed API client with JWT interceptor
├── SellBotLk.Tests/          17 unit tests (HMAC, formatting, Gemini parsing, DTO deserialization)
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
dotnet run                                           # http://localhost:5028 -- Swagger UI in dev
```

### Admin Dashboard

```bash
cd sellbotlk-admin
cp .env.example .env       # defaults to http://localhost:5028
npm install && npm run dev  # http://localhost:5173
```

Default admin login: `admin` / (set `ADMIN_PASSWORD` in your `.env`).

See [`.env.example`](SellBotLk.Api/.env.example) for the full list of required environment variables.
