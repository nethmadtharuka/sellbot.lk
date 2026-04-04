# SellBot.lk — Intelligent WhatsApp Business Automation Platform

> A fully automated, AI-powered business management system built for Sri Lankan SMEs. Handles customer orders, multilingual conversations, payment verification, inventory management, fraud detection, and business reporting — entirely through WhatsApp, without human intervention.

---

## Table of Contents

- [Overview](#overview)
- [Key Features](#key-features)
- [System Architecture](#system-architecture)
- [Technology Stack](#technology-stack)
- [How It Works — Request Flow](#how-it-works--request-flow)
- [AI Design Decisions](#ai-design-decisions)
- [Security Implementation](#security-implementation)
- [Setup and Installation](#setup-and-installation)
- [API Reference](#api-reference)
- [Challenges and Solutions](#challenges-and-solutions)
- [Future Improvements](#future-improvements)
- [Conclusion](#conclusion)

---

## Overview

SellBot.lk is a production-grade WhatsApp business automation platform designed specifically for Sri Lankan furniture and retail businesses. It replaces manual WhatsApp order management — where a shop owner manually reads messages, checks stock, types replies, and tracks payments — with a fully automated AI pipeline that handles every step end-to-end.

**The real-world problem it solves:** Most Sri Lankan small businesses run their entire sales operation through WhatsApp. Owners manually respond to hundreds of messages daily, miss orders, forget stock levels, and have no structured data on their business. SellBot.lk turns WhatsApp into a fully automated storefront with a live admin dashboard, AI-powered conversations, and real-time business intelligence.

**Current scale:** Designed for 50 active users/month on free-tier infrastructure, with a documented scalability path to 5,000+ users with Redis caching, horizontal scaling, and message queuing.

---

## Key Features

### Customer-Facing (WhatsApp)

| Feature | Description |
|---|---|
| **Natural Language Orders** | Customers order in any phrasing — "give me 2 chairs" or "මට පුටු 2ක් ඕනෙ" — processed identically |
| **Visual Product Search** | Customer sends a furniture photo → Gemini Vision finds the 3 most similar products in catalogue |
| **Multilingual Support** | Auto-detects English, Sinhala, and Tamil; replies in the detected language consistently |
| **Price Negotiation** | AI negotiates within business-defined price floors across multiple conversation turns |
| **Payment Slip Verification** | Bank transfer screenshots read and verified automatically via Gemini Vision |
| **Order Status Tracking** | Customer asks for status → real DB lookup returned, not a generated guess |
| **Voice Note Orders** | WhatsApp voice messages transcribed and processed as orders (Sprint 14) |
| **Customer Memory** | Returning customers greeted by name with personalised suggestions based on order history |

### Business Owner (WhatsApp + Dashboard)

| Feature | Description |
|---|---|
| **Daily AI Report** | Automated 7AM WhatsApp report: revenue, top products, anomalies, stock alerts |
| **Fraud Detection** | Orders flagged automatically for velocity, quantity, and value anomalies |
| **Low-Stock Alerts** | Proactive alerts with sales-velocity-based reorder suggestions every 6 hours |
| **Supplier Invoice Processing** | Owner photos a supplier invoice → all line items extracted, inventory auto-restocked |
| **Demand Forecasting** | 7-day product demand forecast based on 90 days of historical sales data |
| **Admin Dashboard** | Live web dashboard — orders, inventory, customers, fraud queue, revenue charts |

---

## System Architecture

SellBot.lk uses a **Layered Architecture** within a single ASP.NET Core 8 project, with strict separation between presentation, application, domain, infrastructure, and integration layers.

```
┌─────────────────────────────────────────────────────────┐
│                   META WHATSAPP API                      │
│              (Webhook POST / Send Message)                │
└────────────────────────┬────────────────────────────────┘
                         │ HTTPS + HMAC-SHA256
                         ▼
┌─────────────────────────────────────────────────────────┐
│              PRESENTATION LAYER                          │
│   WhatsAppWebhookController  │  AdminDashboard           │
│   HmacVerificationMiddleware │  ProductsController       │
│   OrdersController           │  CustomersController      │
└────────────────────────┬────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────┐
│              APPLICATION LAYER                           │
│                                                          │
│  MessageProcessingService  ──►  GeminiService            │
│         │                           │                    │
│    ┌────┴──────────────────────┐    │ Gemini 1.5 Flash   │
│    │  OrderService             │    │ REST API           │
│    │  ProductService           │◄───┘                    │
│    │  NegotiationService       │                         │
│    │  FraudDetectionService    │                         │
│    │  PaymentMatchingService   │                         │
│    │  DeliveryService          │                         │
│    │  DocumentService          │                         │
│    │  InventoryService         │                         │
│    │  ReportService            │                         │
│    └────────────┬──────────────┘                         │
└─────────────────┼───────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────┐
│              INFRASTRUCTURE LAYER                        │
│   AppDbContext (EF Core 8 + Pomelo MySQL)                │
│   ProductRepository  │  OrderRepository                  │
│   Hangfire Background Jobs (Reports, Alerts, Forecasts)  │
└─────────────────────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────┐
│                    MySQL 8.0                             │
│  9 tables: Customers, Products, Orders, OrderItems,      │
│  Documents, Conversations, InventoryLogs,                │
│  DailyReports, DeliveryZones                            │
└─────────────────────────────────────────────────────────┘
```

### Database Design Highlights

- **9 normalised tables** with full relational integrity and FK constraints
- `utf8mb4` charset throughout for complete Sinhala and Tamil Unicode support
- JSON columns for `Conversations.Context` (cart state) and `Documents.ExtractedData` (Gemini output) — flexible schema for AI-generated data without sacrificing relational integrity elsewhere
- Insert-only `InventoryLogs` table — immutable audit trail, never updated or deleted
- Indexes on `Orders.CustomerId`, `Orders.Status`, `Orders.CreatedAt`, `Products.Category`, `Products.IsActive` — query performance built in from Sprint 1
- Denormalised `TotalOrders` and `TotalSpend` on `Customers` — a deliberate performance trade-off for dashboard summary cards

---

## Technology Stack

| Layer | Technology | Reason |
|---|---|---|
| **Backend** | .NET 8 / ASP.NET Core | Industry-standard, strong typing, excellent DI container |
| **AI / NLP** | Google Gemini 1.5 Flash | Generous free tier, multilingual, vision API, fast response |
| **Database** | MySQL 8.0 + EF Core 8 | Free community edition, widely deployed in Sri Lanka |
| **ORM** | Pomelo.EntityFrameworkCore.MySql | Only production-ready MySQL EF Core provider |
| **Messaging** | Meta WhatsApp Business Cloud API | Only official API for WhatsApp automation at scale |
| **Background Jobs** | Hangfire + MySQL storage | Persistent job queue with built-in retry and dashboard |
| **Logging** | Serilog + structured logging | Correlation IDs, masked PII, multiple sinks |
| **API Docs** | Swashbuckle / Swagger UI | Auto-generated from controller attributes with JWT support |
| **PDF Processing** | PdfPig | Free, no licensing restrictions |
| **Deployment** | Railway / Docker | Free-tier hosting with environment variable injection |
| **CI/CD** | GitHub Actions | Automated build → test → deploy pipeline |

---

## How It Works — Request Flow

### Incoming Customer Message (Text Order)

```
1.  Customer sends WhatsApp message → Meta Cloud API fires POST to /api/v1/webhook/whatsapp

2.  HmacVerificationMiddleware intercepts → computes HMAC-SHA256 of raw request body
    using WhatsApp App Secret → compares to X-Hub-Signature-256 header
    → Mismatch: HTTP 401, message dropped, attack logged
    → Match: request continues to controller

3.  WhatsAppWebhookController deserialises payload → extracts message, sender phone, name

4.  MessageProcessingService.ProcessTextMessageAsync called:
    a. GetOrCreateCustomerAsync — upserts Customer record, preserves PreferredLanguage
    b. GetOrCreateConversationAsync — loads state; resets if >2 hours inactive
    c. BuildCatalogueSummaryAsync — fetches active product list from MySQL
    d. GeminiService.ParseMessageAsync — sends: message + customer name +
       conversation context + FULL PRODUCT CATALOGUE to Gemini 1.5 Flash
       → Returns: { intent, language, orderItems, productSearchQuery,
                    orderNumber, offeredPrice, replyMessage, confidence }

5.  Intent routed to dedicated handler:
    → Order:            OrderService.CreateOrderAsync (DB transaction, stock deduction)
    → ProductSearch:    ProductService.SmartSearchAsync (Gemini-powered catalogue match)
    → PriceNegotiation: NegotiationService.EvaluateOfferAsync (MinPrice floor logic)
    → OrderStatus:      Direct DB lookup by customerId or orderNumber — NO AI hallucination
    → DeliveryInfo:     DeliveryZones table query — real fees, real ETAs
    → Complaint:        Logged, owner alerted via WhatsApp
    → Greeting:         Personalised reply based on TotalOrders history

6.  WhatsAppSendService.SendTextMessageAsync → POST to Meta Graph API v22.0

7.  Full interaction logged via Serilog with correlation ID, masked phone, intent, latency
```

### Incoming Image Message (Payment Slip vs Product Photo)

```
1.  Webhook receives image message → MediaDownloadService fetches file via Meta Graph API
    (two-step: get download URL → download bytes)

2.  IsPaymentContextAsync checks:
    → Does customer have a confirmed Unpaid order? → PaymentMatchingService
    → Otherwise → MessageProcessingService.ProcessImageMessageAsync (visual search)

3a. Payment path: GeminiVisionService.ExtractPaymentDataAsync
    → Extracts: amount, reference, bank, date, confidence, authenticityScore
    → Reference uniqueness check — rejects if already used on another order
    → Amount tolerance check — exact match required under LKR 5,000
    → Date validation — rejects if slip predates the order
    → High-value threshold — orders > LKR 50,000 require manual owner approval

3b. Visual search path: VisualSearchService.SearchByImageAsync
    → Gemini Vision extracts: type, color, material, style
    → Similarity scored against Products table attributes
    → Top 3 matches returned with prices and stock status
```

---

## AI Design Decisions

AI was used deliberately and with control — not as a black box. These are the key architectural decisions around Gemini:

**1. Gemini classifies intent; the database answers questions.**
When a customer asks "what's my order status?", Gemini detects the `OrderStatus` intent and extracts the order number. The actual answer — status, payment state, delivery area — comes from a direct EF Core query. Gemini never fabricates business data.

**2. Product catalogue is injected into every intent parse call.**
Without the catalogue, Gemini guesses product names. With it, it extracts `"Oak Dining Chair Set"` (your exact product name) instead of a vague `"dining chair"` that fails the DB match. This was the single biggest reliability improvement.

**3. Two separate Gemini call paths.**
`GenerateReplyAsync` wraps prompts in a customer-chat persona ("Be friendly, reply in {language}, no JSON"). `CallGeminiRawAsync` sends raw prompts for structured data tasks. Mixing these caused `SmartSearchAsync` to refuse returning JSON arrays — a subtle but critical bug.

**4. Multi-strategy product matching as a safety net.**
After Gemini extracts a product name, a 5-tier matching cascade runs: exact match → name contains → reverse contains → category match → word overlap scoring. This handles real-world input like "dining chair" matching `"Oak Dining Chair Set"` when Gemini's extraction isn't perfect.

**5. Gemini safety block handling.**
`finishReason: SAFETY` and empty `candidates[]` arrays are treated as recoverable errors — a fallback intent is returned and the customer always gets a reply. Previously these were silent failures.

**6. Prompt injection prevention.**
Customer message text is wrapped as `"untrusted input"` in the prompt with an explicit instruction that it should only be classified, never executed as an instruction. This prevents `"Ignore all previous instructions"` attacks.

---

## Security Implementation

Security was treated as a first-class requirement from Sprint 1, not an afterthought.

| Layer | Implementation |
|---|---|
| **Webhook Authentication** | HMAC-SHA256 signature verification on every incoming POST — any request without a valid `X-Hub-Signature-256` header is rejected with HTTP 401 before reaching any controller |
| **JWT Authentication** | Admin dashboard protected with Bearer token auth, 8-hour expiry, refresh token in HttpOnly cookie |
| **Role-Based Access** | `SuperAdmin`, `BusinessOwner`, `ViewOnly` roles enforced via `[Authorize(Roles=...)]` |
| **SQL Injection Prevention** | All queries via EF Core parameterised LINQ — no raw SQL with user input anywhere |
| **Input Sanitisation** | All WhatsApp message text sanitised before Gemini and DB operations |
| **Secret Management** | API keys, DB passwords, JWT secret in environment variables — never in source code; `.gitignore` enforced from commit 1 |
| **PII Protection** | Phone numbers masked in all logs (`+94771***567`) — full number never written to log files |
| **Rate Limiting** | Admin API: 100 req/min per IP; Gemini calls tracked and throttled within free tier limits |
| **Payment Fraud Prevention** | Authenticity scoring on payment slips, reference number uniqueness check, date validation, high-value manual review threshold |
| **Security Headers** | `X-Content-Type-Options`, `X-Frame-Options`, `Strict-Transport-Security`, `Content-Security-Policy` on all responses |
| **File Validation** | Uploads validated for MIME type and capped at 10MB — no arbitrary file acceptance |

---

## Setup and Installation

### Prerequisites

- .NET 8 SDK
- MySQL 8.0
- ngrok (for local webhook testing)
- Meta Developer account with WhatsApp Business API access
- Google AI Studio account (Gemini API key)

### 1. Clone and Configure

```bash
git clone https://github.com/yourusername/SellBotLk.git
cd SellBotLk
```

Create `SellBotLk.Api/appsettings.Development.json` (this file is gitignored):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=sellbotlk;User=root;Password=yourpassword;CharSet=utf8mb4;"
  },
  "Gemini": {
    "ApiKey": "your-gemini-api-key",
    "Model": "gemini-1.5-flash"
  },
  "WhatsApp": {
    "Token": "your-meta-access-token",
    "PhoneNumberId": "your-phone-number-id",
    "VerifyToken": "your-custom-verify-token",
    "AppSecret": "your-meta-app-secret"
  },
  "Owner": {
    "Phone": "94771234567"
  },
  "Jwt": {
    "Secret": "your-256-bit-secret-key-minimum-32-characters"
  }
}
```

### 2. Database Setup

```bash
cd SellBotLk.Api
dotnet ef database update
```

This runs all migrations and seeds:
- 25 Sri Lankan delivery zones
- Sample product catalogue

### 3. Run the API

```bash
dotnet run
# API available at http://localhost:5028
# Swagger UI at http://localhost:5028/swagger
# Health check at http://localhost:5028/health
```

### 4. Expose Locally with ngrok

```bash
ngrok http 5028
# Copy the https://xxxx.ngrok.io URL
```

### 5. Register Webhook with Meta

1. Go to Meta Developer Console → WhatsApp → Configuration
2. Set Webhook URL to: `https://xxxx.ngrok.io/api/v1/webhook/whatsapp`
3. Set Verify Token to match `WhatsApp:VerifyToken` in your config
4. Subscribe to `messages` events
5. Click Verify — your API must be running for this to succeed

### 6. Admin Dashboard

Open `dashboard.html` directly in Chrome (`Ctrl+O`). Add CORS support to `Program.cs`:

```csharp
builder.Services.AddCors(o => o.AddPolicy("Dashboard", p =>
    p.WithOrigins("null", "http://localhost")
     .AllowAnyMethod().AllowAnyHeader()));

app.UseCors("Dashboard"); // before app.UseAuthorization()
```

### Running Tests

```bash
dotnet test
```

---

## API Reference

All endpoints are prefixed `/api/v1`. Protected endpoints require `Authorization: Bearer <token>`.

### Webhook

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/webhook/whatsapp` | None | Meta hub.challenge verification |
| `POST` | `/webhook/whatsapp` | HMAC-SHA256 | Receive all incoming WhatsApp messages |

### Orders

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/orders` | JWT | List orders with optional `?status=` filter |
| `GET` | `/orders/{id}` | JWT | Single order with full item details |
| `POST` | `/orders` | JWT | Manually create order from dashboard |
| `PUT` | `/orders/{id}/status` | JWT | Update order status |
| `PUT` | `/orders/{id}/cancel` | JWT | Cancel order and restore stock atomically |
| `GET` | `/orders/stats` | JWT | Aggregated statistics for dashboard |

### Products

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/products` | JWT | All active products |
| `POST` | `/products` | JWT | Add new product |
| `PUT` | `/products/{id}` | JWT | Update product details or price |
| `DELETE` | `/products/{id}` | JWT | Soft delete (sets IsActive = false) |
| `POST` | `/products/search` | JWT | Text search |
| `POST` | `/products/visual-search` | JWT | Image-based search via Gemini Vision |
| `GET` | `/products/low-stock` | JWT | All products below threshold |

### Customers

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/customers` | JWT | All customers with order summary |
| `GET` | `/customers/{id}` | JWT | Customer profile with full history |
| `PUT` | `/customers/{id}/block` | JWT | Block or unblock a customer |

### Other Endpoints

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/inventory` | JWT | Full inventory status |
| `POST` | `/inventory/restock` | JWT | Manual stock addition |
| `GET` | `/inventory/alerts` | JWT | Active low-stock alerts |
| `GET` | `/delivery-zones` | JWT | All zones with fees and ETAs |
| `GET` | `/reports/daily` | JWT | Daily report by date |
| `GET` | `/reports/forecast` | JWT | 7-day demand forecast |
| `GET` | `/health` | None | System health check |
| `GET` | `/health/db` | None | MySQL connectivity check |

---

## Challenges and Solutions

### 1. Gemini Returning Inconsistent JSON

**Problem:** Gemini sometimes wraps JSON responses in markdown code blocks (` ```json `) and sometimes returns plain JSON. Deserialisation failed unpredictably.

**Solution:** A `CleanJsonResponse()` method strips markdown fencing and extracts the outermost `{...}` block before deserialisation. All Gemini calls pass through this before any `JsonSerializer.Deserialize` call.

### 2. AI Hallucinating Business Data

**Problem:** Intents like `OrderStatus` and `DeliveryInfo` fell into a generic handler that sent Gemini's `replyMessage` — a fabricated answer with no real data. A customer asking "where's my order?" got a convincing but completely invented status.

**Solution:** Every intent now has a dedicated handler backed by a real database query. Gemini's role is strictly intent classification and language detection. It never answers business questions directly.

### 3. Mixed-Language Messages Breaking Intent Detection

**Problem:** Sri Lankan customers commonly write mixed Sinhala-English — `"chair ekak denna"` (give me a chair). Gemini classified these as `Other` because it couldn't confidently identify the intent.

**Solution:** Explicit rules added to the classification prompt: a list of Sinhala action words (`දෙන්න`, `ගන්න`) and English equivalents mapped to their intents, with a hard rule: *"Never return Other for a message that clearly expresses a product or order intent."*

### 4. Product Name Matching Failures

**Problem:** Gemini extracted `"dining chair"` from a message but the product was `"Oak Dining Chair Set"`. A single `.Contains()` check failed, and the customer got an "item not found" error.

**Solution:** A 5-strategy matching cascade: exact → name contains search → search contains name → category match → word overlap scoring. Multiple strategies run in priority order; the first match wins.

### 5. All Images Routed to Payment Verification

**Problem:** Every image message — product photos, furniture pictures, random images — was sent to `PaymentMatchingService`. Gemini attempted to read every image as a bank transfer slip. Visual search was never reachable.

**Solution:** Context-aware routing in the webhook controller. The system checks if the customer has an active unpaid confirmed order. If yes → payment slip flow. If no → visual search flow. No customer action required to switch modes.

### 6. Caching Serving Wrong Customer Replies

**Problem:** Gemini responses were cached by `messageText.GetHashCode()` only. Two customers sending `"hello"` within the cache window received the same cached reply — including the wrong customer's name.

**Solution:** Cache key now incorporates `customerName + messageText + conversationContext[..50]`. Each customer's context produces a distinct cache entry.

### 7. Silent Failures on Exceptions

**Problem:** Any unhandled exception in message processing caused the webhook to return HTTP 200 to Meta (correct) but send no reply to the customer. From the customer's perspective, the bot was broken.

**Solution:** Three-layer exception safety net. `MessageProcessingService` wraps all processing in try/catch and sends a recovery message on failure. The webhook controller has a per-message try/catch so one bad message doesn't prevent others in the same batch from being processed.

### 8. GitGuardian Flagged Real Credentials

**Problem:** A MySQL password was committed inside `.env.example` during Sprint 1 setup. GitGuardian bot flagged it on the pull request.

**Solution:** Immediately invalidated the exposed credential, ran `git rebase -i` to squash and rewrite history, force-pushed to overwrite the remote. Added `appsettings.Development.json` to `.gitignore` before creating the file on all subsequent environments. Documented the pattern in the team README.

---

## Future Improvements

| Priority | Improvement | Impact |
|---|---|---|
| High | **PayHere payment gateway integration** — replace slip verification with real-time payment confirmation via webhook | Eliminates payment fraud entirely |
| High | **Redis caching** — product catalogue with 5-minute TTL, replace IMemoryCache | Required for scaling beyond 500 users |
| High | **JWT admin authentication** — complete the login flow with refresh tokens | Security requirement before production |
| Medium | **RabbitMQ message queue** — async webhook processing to prevent blocking under load | Required for 2,000+ user scale |
| Medium | **Voice note processing** — Gemini Audio API transcription for voice orders | High demand from Sri Lankan users |
| Medium | **Facebook Messenger channel** — second messaging channel using same backend | Doubles addressable market |
| Medium | **LankaQR per-order payment** — Central Bank QR standard, supported by all Sri Lankan banks | Eliminates payment slip workflow |
| Low | **MySQL read replica** — separate read traffic from write traffic | Needed at 2,000+ concurrent users |
| Low | **Multi-business support** — tenant isolation for white-labelling to other businesses | Commercial product opportunity |
| Low | **Mobile admin app** — React Native dashboard for owner on the go | Improved owner experience |

---

## Conclusion

SellBot.lk demonstrates that a focused AI integration — built on solid engineering foundations — can solve a real, measurable business problem for an underserved market.

The project makes deliberate architectural choices at every layer: AI is controlled and bounded (intent classification only, never data retrieval), security is layered from the webhook inward (HMAC verification, input sanitisation, parameterised queries, secret management), and the database schema is designed for the long term (audit logs, soft deletes, denormalised counters for performance).

What makes this project technically substantive is not the use of AI — it is the engineering around the AI: handling its inconsistencies, compensating for its limitations, and ensuring that a real business can rely on it without constant human oversight.

The system is fully functional at production scale for a 50-user Sri Lankan furniture business, deployed on free-tier infrastructure, with a documented and costed path to 5,000 users. It processes real WhatsApp messages, manages real inventory, and generates real business intelligence — today.

---

*Built with .NET 8, Google Gemini 1.5 Flash, MySQL 8.0, and Meta WhatsApp Business API*

*SellBot.lk — March 2026*
