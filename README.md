## SellBotLk

WhatsApp-based commerce platform for a Sri Lankan furniture business. Customers chat via WhatsApp to browse products, place orders, send payment slips, and track delivery — powered by Gemini AI for natural language understanding in English, Sinhala, and Tamil.

### Tech Stack

| Layer | Technology |
|-------|-----------|
| API | ASP.NET Core 8 (REST + WhatsApp webhook) |
| AI | Google Gemini 2.5 Flash (intent parsing, product search, image analysis) |
| Database | MySQL 8 + Entity Framework Core |
| Admin | React 19 + TypeScript + Vite |
| CI | GitHub Actions (build + lint + test) |

### Architecture

```
WhatsApp ──webhook──> SellBotLk.Api ──> Gemini AI
                          │
                     MySQL (EF Core)
                          │
               sellbotlk-admin (React) ──REST──┘
```

### Key Features

- **Conversational commerce** — Natural language order placement via WhatsApp
- **Multi-language** — English, Sinhala, Tamil with auto-detection
- **AI product search** — Text and visual (image) product matching via Gemini
- **Payment verification** — Customers send payment slip photos for automated matching
- **Order lifecycle** — Full status tracking (Pending → Confirmed → Dispatched → Delivered)
- **Delivery zones** — Zone-based delivery management
- **Admin dashboard** — Order management, filtering, and detail views
- **Webhook security** — HMAC-SHA256 signature verification on all incoming webhooks

### Projects

| Project | Path | Description |
|---------|------|-------------|
| API | `SellBotLk.Api/` | Backend — REST endpoints + WhatsApp webhook handler |
| Admin | `sellbotlk-admin/` | React admin dashboard |
| Tests | `SellBotLk.Tests/` | xUnit + Moq + FluentAssertions |

### Prerequisites

- .NET 8 SDK
- Node.js 20+
- MySQL 8

### Run Locally

#### 1) API

Configure your environment — copy `.env.example` and fill in your keys:

```bash
cp SellBotLk.Api/.env.example SellBotLk.Api/.env
```

Or set values in `appsettings.Development.json` (excluded from git).

```bash
cd SellBotLk.Api
dotnet run
```

API runs at `http://localhost:5028`. Swagger UI is available in Development mode.

#### 2) Admin Dashboard

```bash
cd sellbotlk-admin
cp .env.example .env    # adjust VITE_API_BASE_URL if needed
npm install
npm run dev
```

Dashboard runs at `http://localhost:5173`.

### CORS

The API allows requests from the admin dashboard origins:

- `http://localhost:5173`
- `http://127.0.0.1:5173`

### CI

GitHub Actions runs on every push and PR:

- **Admin** — `npm ci` → `npm run build` → `npm run lint`
- **API** — `dotnet build` → `dotnet test`
