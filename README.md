# ASMS — Automated Service Management System

ASMS is a home-services platform. Customers book jobs, and admins or a background engine assign technicians. Technicians bill the work, and customers pay through Razorpay. The platform also covers:

- disputes and refunds
- promo codes, loyalty points and referrals
- technician wallets and withdrawals
- live tracking

## Architecture

```
                 ┌──────────────────────┐        ┌──────────────────────┐
  Next.js  ───►  │   ServiceApp.Api     │        │   ServiceApp.Web     │  ◄── Browsers (Razor MVC)
  (frontend/)    │  REST /api/*  (JWT)  │        │  Areas Admin/Customer│
  Flutter  ───►  │  SignalR /hubs/service│        │  /Technician (cookie)│
                 │  Hangfire SERVER +   │        │  SignalR /hubs/service│
                 │  recurring jobs      │        │  Hangfire client only│
                 │  Swagger, /health    │        │  /health             │
                 └──────────┬───────────┘        └──────────┬───────────┘
                            │      both call AddServiceAppCore(...)      │
                            └───────────────┬────────────────────────────┘
                                            ▼
                         ┌─────────────────────────────────────┐
                         │ ServiceApp.Hosting (shared compose) │  DI, Identity, Serilog,
                         │  ServiceHub + SignalR notifications │  Hangfire storage, seeding,
                         │  optional Redis backplane           │  health checks
                         └───────┬───────────────┬─────────────┘
                                 ▼               ▼
                 ┌────────────────────┐   ┌──────────────────────────┐
                 │ ServiceApp.Services│   │ ServiceApp.Infrastructure│
                 │ business logic     │   │ Razorpay, SendGrid,      │
                 └─────────┬──────────┘   │ QuestPDF bill PDFs       │
                           ▼              └────────────┬─────────────┘
                 ┌────────────────────┐                │
                 │ ServiceApp.Data    │ EF Core 8,     │
                 │ DbContext, repos,  │ UnitOfWork,    │
                 │ migrations         │ SQL Server     │
                 └─────────┬──────────┘                │
                           ▼                           ▼
                 ┌──────────────────────────────────────────────┐
                 │ ServiceApp.Core — entities, enums, interfaces │  (references nothing)
                 └──────────────────────────────────────────────┘
```

| Project | Responsibility |
|---|---|
| `ServiceApp.Core` | Entities, enums, service and repository interfaces, `Result`/`ErrorType`, settings POCOs |
| `ServiceApp.Data` | `ApplicationDbContext`, repositories, `UnitOfWork`, EF migrations |
| `ServiceApp.Services` | Domain services: requests, billing, payments, wallet, promotions, disputes, auto-assignment, accounts |
| `ServiceApp.Infrastructure` | Razorpay, SendGrid and QuestPDF adapters |
| `ServiceApp.Hosting` | Shared composition used by both hosts: `AddServiceAppCore`, `AddServiceAppRealtime`, `AddServiceAppHangfireStorage`, Serilog, seeding |
| `ServiceApp.Api` | Web API host: JWT plus refresh tokens, ProblemDetails, rate limiting, CORS, Swagger, Razorpay webhook, Hangfire server |
| `ServiceApp.Web` | MVC host: cookie auth and Razor Areas. It can enqueue Hangfire jobs but never processes them |
| `tests/ServiceApp.Tests` | xUnit regression tests (EF Core InMemory and Moq) |
| `frontend/` | Next.js client (see `frontend/README.md`) |

API reference, including the auth flow and SignalR events: **[docs/API.md](docs/API.md)**.

### Realtime across both hosts

Each host maps the same `ServiceHub` at `/hubs/service`. Clients join groups from their authenticated claims.

- **Without Redis (default):** a notification reaches only the clients connected to the host that raised it.
  - Auto-assign runs in the API (Hangfire), so its events reach only API/Next.js/Flutter clients.
  - MVC actions notify only MVC browsers.
- **With Redis:** set `SignalR:RedisConnectionString` on **both** hosts. They then share a backplane (channel prefix `asms`), and every event reaches every client. The trade-off is one more piece of infrastructure, plus a small latency hop per message.

## Prerequisites

- .NET SDK 8 or later, with the ASP.NET Core 8 runtime
- SQL Server: LocalDB, Developer or Express
- `dotnet-ef` for migrations: `dotnet tool install -g dotnet-ef`
- Node.js 20+ for the frontend
- Optional: Redis, for the SignalR backplane

## Secrets (user-secrets)

Both hosts share the UserSecretsId **`asms-shared-secrets`**, so you set each secret once. `appsettings.json` contains placeholders only.

```bash
cd ServiceApp.Api   # or ServiceApp.Web — same secret store
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<sql connection string>"
dotnet user-secrets set "Jwt:Key"                "<random string, at least 32 bytes>"
dotnet user-secrets set "Razorpay:KeyId"         "<rzp key id>"
dotnet user-secrets set "Razorpay:KeySecret"     "<rzp key secret>"
dotnet user-secrets set "Razorpay:WebhookSecret" "<webhook secret from Razorpay dashboard>"
dotnet user-secrets set "SendGrid:ApiKey"        "<sendgrid api key>"
dotnet user-secrets set "SendGrid:FromEmail"     "<verified sender>"
dotnet user-secrets set "Seed:AdminPassword"     "<first admin password>"     # only needed for a fresh DB
# optional
dotnet user-secrets set "Notifications:AdminEmail"       "<admin alert inbox>"
dotnet user-secrets set "SignalR:RedisConnectionString"  "<redis connection string>"
```

When a secret is missing, startup logs a warning naming the key; the value is never logged. The API refuses to start if `Jwt:Key` is missing or shorter than 32 bytes.

Other settings are in `appsettings.json` and can be overridden with environment variables, for example `Cors__AllowedOrigins__0`:

| Key | Default | Notes |
|---|---|---|
| `Cors:AllowedOrigins` | `["http://localhost:3000"]` | API only. In Development, loopback origins are also allowed |
| `Jwt:AccessTokenMinutes` / `Jwt:RefreshTokenDays` | 10080 / 30 | Lower the access-token lifetime once every client uses refresh |
| `Database:MigrateOnStartup` | `true` in the API's Development settings | Only the API migrates, so the two hosts never race |
| `AutoAssignment:AllowOutOfZoneFallback` | `false` | When `true`, a technician outside the request's zone can be auto-assigned |
| `Seed:AdminEmail` | `admin@serviceapp.com` | The first admin is created only when `Seed:AdminPassword` is set |
| `DataProtection:KeysPath` | (none) | Shared key ring for multi-machine deployments. The admin cookie works on the API's `/hangfire` |

## Database

```bash
dotnet ef database update --project ServiceApp.Data --startup-project ServiceApp.Web
# new migration:
dotnet ef migrations add <Name> --project ServiceApp.Data --startup-project ServiceApp.Web
```

## Running everything locally

Run each host in its own terminal:

```bash
dotnet run --project ServiceApp.Api --launch-profile https   # https://localhost:7001 (http 5001) — Swagger at /swagger
dotnet run --project ServiceApp.Web --launch-profile https   # https://localhost:7002 (http 5002) — MVC site
cd frontend && npm install && npm run dev                    # http://localhost:3000 — Next.js
```

- **Hangfire.** The dashboard is at `https://localhost:7001/hangfire`. It accepts an Admin JWT, or the admin's MVC login cookie (same host name).
- **Auto-assign.** The job runs only while the API is running.
- **Health checks.** Both hosts expose `/health`.

## Tests

```bash
dotnet test Automated-Service-Management-System.slnx
```
