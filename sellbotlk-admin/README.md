# SellBotLk Admin Dashboard

Admin panel for managing the SellBotLk WhatsApp commerce platform. Built with React, TypeScript, and Vite.

## Features

- **Orders** — View, filter by status/customer, and drill into order details
- **Order Details** — Inspect line items, payment status, delivery info, and update order state
- **Delivery Zones** — Manage delivery areas and zone configuration

## Tech Stack

- React 19 + TypeScript
- React Router v7
- Vite 8 (dev server + build)
- ESLint with TypeScript rules

## Getting Started

### Prerequisites

- Node.js 20+
- The SellBotLk API running locally (default `http://localhost:5028`)

### Setup

```bash
# Install dependencies
npm install

# Copy and configure environment
cp .env.example .env
# Edit .env if your API runs on a different port

# Start dev server
npm run dev
```

The dashboard will be available at `http://localhost:5173`.

### Available Scripts

| Script | Description |
|--------|-------------|
| `npm run dev` | Start Vite dev server with HMR |
| `npm run build` | Type-check and build for production |
| `npm run lint` | Run ESLint |
| `npm run preview` | Preview production build locally |

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `VITE_API_BASE_URL` | `http://localhost:5028` | Base URL of the SellBotLk API |
