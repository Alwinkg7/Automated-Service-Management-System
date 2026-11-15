# ASMS frontend

A Next.js 16 (App Router), TypeScript (strict) and Tailwind CSS v4 frontend for the ASMS backend (`ServiceApp.Api`). It covers the three roles (Customer, Technician and Admin), a public landing page and SEO pages for each service.

## Quick start

```bash
cd frontend
cp .env.example .env.local      # adjust if the API is not on http://localhost:5001
npm install
npm run dev                     # http://localhost:3000
```

Run the API alongside it with `dotnet run --project ServiceApp.Api` (http://localhost:5001). The API's `Cors:AllowedOrigins` must include `http://localhost:3000`, which it does by default. The browser needs CORS only for the SignalR hub.

| Variable               | Used by          | Purpose                                                                       |
| ---------------------- | ---------------- | ----------------------------------------------------------------------------- |
| `NEXT_PUBLIC_API_URL`  | browser + server | API origin. The SignalR hub is `${NEXT_PUBLIC_API_URL}/hubs/service`          |
| `API_URL`              | server only      | API origin as seen from the Next.js server. Defaults to `NEXT_PUBLIC_API_URL` |
| `NEXT_PUBLIC_SITE_URL` | server           | Canonical URLs, sitemap and Open Graph                                        |

No secrets live in the frontend. The Razorpay **key id** comes from the API's order response.

## Scripts

| Command                    | What it does                                                        |
| -------------------------- | ------------------------------------------------------------------- |
| `npm run dev`              | Dev server on port 3000                                             |
| `npm run build`            | Production build, including the type check                          |
| `npm run lint`             | ESLint (next/core-web-vitals + TypeScript + Prettier compatibility) |
| `npm run typecheck`        | `tsc --noEmit`                                                      |
| `npm test`                 | Vitest + React Testing Library                                      |
| `npm run test:integration` | Live API + BFF contract suite (see Testing)                         |
| `npm run format`           | Prettier, with the Tailwind class sorter                            |

## How auth works (BFF)

```
Browser ──► /api/auth/login (Next route handler) ──► API /api/auth/login
        ◄── Set-Cookie: asms_token (httpOnly), asms_refresh (httpOnly, SameSite=Strict)

Browser ──► /api/proxy/api/customer/... ──► API /api/customer/...   (+ Authorization: Bearer <cookie>)
```

- **Tokens never reach JavaScript.** Our route handlers set the access and refresh tokens as httpOnly cookies. All browser API traffic goes through `src/app/api/proxy/[...path]/route.ts`, which attaches the Bearer token. It only forwards `/api/*` and rejects cross-origin mutating requests.
- **Refresh:** the proxy, `src/proxy.ts` and the SignalR token route renew silently with `POST /api/auth/refresh` when the access token is expired or rejected. The refresh token rotates, and the new pair is stored.
- **Route protection:** `src/proxy.ts` (Next 16's replacement for `middleware.ts`) decodes the JWT, without verifying it, to route users. Signed-out users go to `/login?next=…`, and users with the wrong role go to `/403`. Role layouts call `requireRole()` again on the server. **The API remains the real enforcement.** The rules live in `src/lib/auth/guard.ts` and have unit tests.
- **SignalR:** this is the one exception. WebSockets can't use the proxy, so `GET /api/auth/token` (same-origin) hands the current access token to `accessTokenFactory`. The hub reads it as `access_token`.
- **Logout** revokes the refresh token on the API, clears the cookies, stops the hub connection and clears the query cache.

## Architecture

```
src/
  app/
    (public)/            landing, /services, /services/[category] (SSG + FAQ JSON-LD)
    (auth)/              /login, /register
    customer/ technician/ admin/   role areas (layout = RoleLayout: auth + shell + realtime)
    api/auth/*           BFF auth handlers      api/proxy/[...path]   BFF API proxy
    sitemap.ts robots.ts 403/ not-found.tsx error.tsx
  components/
    ui/                  design system (Button, Field, Card, Badge/StatusBadge, Modal, DataTable, QueryView…)
    layout/              AppShell, PageHeader, PublicHeader, NotificationBell, ThemeToggle
    features/            bill/, request/, dispute/, tracking/, charts/, admin/
    providers/           AppProviders (Query, theme, toasts), SessionProvider, RealtimeProvider
  lib/
    api/client.ts        the single fetch wrapper (envelope + ProblemDetails, zod validation, ApiError)
    api/endpoints/*      typed endpoint functions per role
    api/schemas.ts       zod schemas = response types
    auth/                cookies, JWT decode, guard rules, server session helpers
    realtime/            the shared SignalR connection
  hooks/                 useAction (mutation + toast + invalidate), useHubEvent, useUrlState, useLocationSharing…
  types/enums.ts         mirrors ServiceApp.Core/Enums
```

### Rendering and data

- Public pages are **static** (SSG). Signed-in pages are dynamic: the role layout reads the session cookie on the server.
- Per-user data uses **TanStack Query** in Client Components through the proxy. Lists have a 30 s stale time and retry only on 5xx or network errors. Query keys live in `src/lib/queryKeys.ts`.
- **Realtime:** every hub event shows a toast, adds to the bell badge and invalidates the role's queries. Lists and details refresh themselves, with no manual patching.
- Filters and search live in **URL search params**, so they can be shared and work with the back button.
- `cacheComponents` is off on purpose. Every authenticated route reads cookies, and the classic dynamic model is simpler.

### Error handling

`ApiError` understands the legacy `{ success:false, error }` body and RFC 7807 ProblemDetails, including `errors` field maps. Field errors are mapped onto forms, and 401 sends the user to login. A 409 refetches the affected queries and explains the conflict. A 429 reads `Retry-After`. For 5xx responses the user sees a generic message, never raw server text. Each role area has `loading.tsx` and `error.tsx`. Every query renders skeleton → error (with retry) → empty → data through `QueryView`.

### Payments

The flow is order (amount set by the server) → Razorpay Checkout → `verify` (the server checks the HMAC signature and binds the order to the bill). The frontend never sets the amount and never marks a bill paid. The checkout script loads only on the pay page, and the button stays busy across the whole flow to prevent double payment.

### Double-booking guard

The booking form sends an `Idempotency-Key` header. The key is generated once per attempt and reused on retry. The submit button is disabled while the request is in flight.

## API alignment

All screens use endpoints documented in `docs/API.md`. [`docs/api-requests.md`](docs/api-requests.md) records the contract differences the frontend adopted, what the live integration run verified, and the remaining non-blocking backend notes.

## Testing

`npm test` runs Vitest with jsdom:

- `src/lib/api/client.test.ts`: envelope unwrapping, ProblemDetails parsing, 5xx message hiding, `Retry-After`, 401 hook, network errors, schema mismatch, 204.
- `src/lib/auth/guard.test.ts`: route protection decisions, expired tokens, wrong role, open-redirect protection, JWT claim decoding.
- `src/lib/format.test.ts`: API date parsing and CSV escaping, including the formula-injection guard.
- `src/app/(auth)/login/LoginForm.test.tsx`: login form validation, safe redirect, and error display (React Testing Library).

### Live integration suite

`npm run test:integration` (`src/integration/flow.int.test.ts`) drives the real typed endpoint functions and zod schemas through the Next.js BFF against a running API. It covers every role: register, login, refresh, logout, booking with an idempotency key, assignment, billing, quote/promo/points, Razorpay order, cash, disputes, withdrawals and the admin pages. It creates `*@asms.test` users in the target database.

```bash
# 1. API on :5001 against a dev database (env override only; user-secrets untouched).
#    Use fake Razorpay and SendGrid values so nothing is charged or emailed.
ConnectionStrings__DefaultConnection="Server=(localdb)\AsmsQa;Database=ServiceAppDB_Dev;Trusted_Connection=True;TrustServerCertificate=True" Razorpay__KeyId=rzp_test_fake Razorpay__KeySecret=fake SendGrid__ApiKey=SG.invalid Seed__AdminEmail=fe-admin@asms.test Seed__AdminPassword='<choose one>' ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5001 dotnet run --project ServiceApp.Api --no-launch-profile
# 2. Frontend
npm run build && npm start
# 3. Suite
INT_ADMIN_EMAIL=fe-admin@asms.test INT_ADMIN_PASSWORD='<same>' npm run test:integration
```
