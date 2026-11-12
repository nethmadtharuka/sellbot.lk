# SellBot.lk Console (frontend)

React 19 + TypeScript + Vite admin console and landing page for SellBotLK, built to the approved designs in `../design`.

## Run

```bash
npm install
cp .env.example .env      # VITE_DATA_MODE=demo|live
npm run dev               # http://localhost:5173
```

- **Demo mode** (default): fictional data from `src/data/demo.ts`; nothing calls the API; changes reset on reload.
- **Live mode** (`VITE_DATA_MODE=live`): uses the real API at `VITE_API_BASE_URL` and requires the owner sign-in.
  Screens the backend has no endpoint for (customers, inbox, documents list, customer-language chart) still use the
  mock layer and are marked "Simulated".

## Checks

```bash
npm run typecheck   # tsc -b (strict)
npm run lint
npm test            # node:test via Vite's module loader (no extra packages)
npm run build
```

## Structure

| Path | Purpose |
|---|---|
| `src/styles/tokens.css` | All design tokens (colour, type, spacing, radius, glow, z-index) |
| `src/ui/` | Design-system components, icons (Lucide paths, Simple Icons WhatsApp glyph) |
| `src/layouts/` | Admin shell (sidebar / rail / drawer) and presentation layout |
| `src/pages/` | One file per designed page |
| `src/data/` | `DataSource` interface, `liveSource` (real API), `demoSource` + `demo.ts` (mock layer) |
| `src/assets/fonts/` | Self-hosted Poppins, Inter, Geist Mono, Noto Sans Sinhala/Tamil (OFL) |

Design parity status: see `DESIGN_PARITY.md`.
