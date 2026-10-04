# Eco Meal frontend

React 19 + TypeScript SPA built with Vite. It talks to `NetromEcoMeal.Api` over JWT-authenticated
REST and a SignalR hub (`/hubs/stock`) for live package stock. See
[FRONTEND_ARCHITECTURE.md](../../FRONTEND_ARCHITECTURE.md) for how it is put together.

## Running

```bash
cp .env.example .env.local   # VITE_API_URL=http://localhost:5080/api
npm install
npm run dev                  # http://localhost:5173
```

## Scripts

| Script | What it does |
|---|---|
| `npm run dev` | Vite dev server with HMR |
| `npm run build` | `tsc -b` then a production build into `dist/` |
| `npm run preview` | Serves the production build locally |
| `npm run lint` | Oxlint |
| `npm test` | Vitest (contexts, hooks, utils, route guards) |

## Docker

The `Dockerfile` builds the app and serves it with nginx (`nginx.conf` provides the SPA fallback).
It is wired into `docker-compose.test.yml` at the repo root, which exposes it on
`http://localhost:5174`.
