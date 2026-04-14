## SellBotLk

WhatsApp commerce backend (ASP.NET Core) + admin dashboard (React).

### Apps

- **API**: `SellBotLk.Api` (REST + WhatsApp webhook)
- **Admin dashboard**: `sellbotlk-admin` (Vite + React + TypeScript)

### Run locally

#### 1) API

- Configure DB + keys in `SellBotLk.Api/appsettings.Development.json` (or environment variables).
- Run:

```bash
cd SellBotLk.Api
dotnet run
```

API is typically at `http://localhost:5000` (check your console output). Swagger is enabled in Development.

#### 2) Admin dashboard

Create a local env file (optional):

- Copy `sellbotlk-admin/.env.example` to `sellbotlk-admin/.env`
- Set `VITE_API_BASE_URL` if your API is not `http://localhost:5000`

Run:

```bash
cd sellbotlk-admin
npm install
npm run dev
```

Dashboard dev server is typically `http://localhost:5173`.

### CORS

The API enables a CORS policy named `AdminDashboard` allowing:

- `http://localhost:5173`
- `http://127.0.0.1:5173`

