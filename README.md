# HireUs - Automated-Service-Management-System

HireUs is an ASP.NET Core (.NET 8) service-management web application.  
It is a layered solution (Core, Data, Services, Infrastructure, Web) with support for roles (Admin / Technician / Customer), online payments (Razorpay), background jobs (Hangfire), real-time notifications (SignalR), PDF bills (QuestPDF), and email delivery (SendGrid).

---

## Table of contents

- About
- Architecture & Modules
- Features
- Prerequisites
- Configuration
- Build & Run (Visual Studio & dotnet CLI)
- Database (migrations & seeding)
- Background jobs & real-time features
- Environment / Secrets
- Packaging / Docker (optional)
- Troubleshooting
- Contributing
- License

---

## About

HireUs is a multi-project ASP.NET Core solution that demonstrates a production-like architecture:
- Clean separation between domain (Core), persistence (Data), business logic (Services), integrations (Infrastructure), and the Web UI (Web).
- Extensible DI, EF Core migrations, and tests-ready structure.

---

## Architecture & Modules

Solution contains the following projects:

- `ServiceApp.Core`  
  Domain entities, DTOs, interfaces (e.g., `IUnitOfWork`, `INotificationService`, `IDisputeService`).

- `ServiceApp.Data`  
  EF Core DbContext, entity configurations, and migrations.

- `ServiceApp.Services`  
  Business logic / service implementations (ServiceRequestService, PaymentService, DisputeService, etc.).

- `ServiceApp.Infrastructure`  
  Integrations with external APIs (Razorpay, email helpers, etc.), implementation helpers.

- `ServiceApp.Web`  
  ASP.NET Core Web application (controllers, areas, views, SignalR hubs). Razor Pages / MVC views for Admin, Customer, Technician areas.

---

## Key libraries & tools used

- .NET 8 (Target framework)
- ASP.NET Core (Web)
- Entity Framework Core
- Microsoft.AspNetCore.Identity (Identity + roles)
- Razorpay (.NET SDK & direct REST calls) — payments and refunds
- Hangfire (background job processing)
- SignalR (real-time notifications)
- QuestPDF (bill PDF generation)
- Serilog (structured logging)
- SendGrid (email delivery) — or your configured email provider
- Microsoft SQL Server (or SQL Server Express / Docker image) for production/dev database

Check each project `.csproj` for exact package versions.

---

## Features

- Role-based system: Admin, Technician, Customer
- Service request lifecycle: create, assign, accept, complete
- Billing & online payments (Razorpay) + refund support
- Dispute handling (raise, review, resolve, issue refund)
- PDF generation for bills (QuestPDF)
- Background tasks & scheduling using Hangfire
- Real-time push notifications via SignalR
- Identity-based authentication (cookie) + JWT support for API clients
- Daily-rotating Serilog file logs

---

## Prerequisites

- .NET 8 SDK
- SQL Server (local) or Docker (mssql-server image)
- Visual Studio 2022/2026 or VS Code (recommended)
- (Optional) SendGrid account and Razorpay credentials for payments

---

## Configuration

Configuration is primarily in `ServiceApp.Web/appsettings.json` and user secrets / environment variables for secrets.

Essential configuration sections / keys:

- Connection string :
      "ConnectionStrings": { "DefaultConnection": "Server=.;Database=HireUsDb;Trusted_Connection=True;" }
- JWT:
      "Jwt": { "Key": "<your_jwt_secret>", "Issuer": "HireUs", "Audience": "HireUsClient" }
- Razorpay:
      "Razorpay": { "KeyId": "<razorpay_key_id>", "KeySecret": "<razorpay_key_secret>" }
- SendGrid:
      "SendGrid": { "ApiKey": "<sendgrid_api_key>", "FromEmail": "noreply@yourdomain.com", "FromName": "HireUs" }
- Serilog — configurable in `appsettings.json` (logging levels, file path).

Use dotnet user-secrets for local dev or environment variables in production.

---

## Build & Run

From Visual Studio:
1. Open solution `Automated-Service-Management-System.slnx`.
2. Set `ServiceApp.Web` as startup project.
3. Ensure the database connection string points to a running SQL Server instance.
4. Start (F5) — the app will run, seed roles/admin if needed, and open in a browser.

From command line (PowerShell):From repository root
dotnet restore dotnet build
Apply migrations (see Database section)
cd ServiceApp.Web dotnet run
---

## Database: migrations & seeding

The app includes EF Core migrations in `ServiceApp.Data`. To create/apply migrations:

- Add migration (if you change the model): cd ServiceApp.Data dotnet ef migrations add YourMigrationName --project ../ServiceApp.Data/ServiceApp.Data.csproj --startup-project ../ServiceApp.Web/ServiceApp.Web.csproj
- Apply migrations (update database): dotnet ef database update --project ServiceApp.Data/ServiceApp.Data.csproj --startup-project ServiceApp.Web/ServiceApp.Web.csproj
On startup the application runs `SeedAsync(app)` (Program.cs) to create roles and the default admin account if missing.

---

## Background jobs & real-time (Hangfire / SignalR)

- Hangfire is configured to use the same SQL Server DB. Start the app and Hangfire server runs automatically.
- You can enable the Hangfire dashboard (if configured) to view jobs.
- SignalR hubs are registered in the Web project; clients subscribe to receive notifications.

---

## Environment / Secrets

Local development:
- Use `dotnet user-secrets` in `ServiceApp.Web` directory: dotnet user-secrets init dotnet user-secrets set "Razorpay:KeyId" "<value>" dotnet user-secrets set "Razorpay:KeySecret" "<value>" dotnet user-secrets set "SendGrid:ApiKey" "<value>" dotnet user-secrets set "Jwt:Key" "<value>"
Production:
- Configure environment variables or use your cloud provider secret store (Azure Key Vault, AWS Secrets Manager).

---

## Docker (optional)

You can run SQL Server in Docker for local development: docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Your_password123" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latestUpdate `DefaultConnection` to point at `localhost,1433` and run migrations & app.

---

## Troubleshooting

- CS0246 / missing types: ensure all project references are intact and run `dotnet restore`. If you add package references to a project, confirm package is in the correct project that uses the types.
- Razor parsing RZ1010: ensure Razor views don't contain nested `@{ }` inside another `@{ }` or `@foreach` incorrectly — move inner code to the outer code block.
- Hot Reload / ENC0023: stop debugging and restart the app/VS to allow structural code changes.
- Database connectivity: verify SQL Server is running and connection string credentials are correct.

---

## Running tests

If tests exist, run: dotnet test
(Adjust to the test project name if present.)

---

## Contributing

1. Fork the repo
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Make changes and add tests if applicable
4. Submit a pull request with a clear description

Coding style:
- Follow C# conventions, nullable enabled, and keep DI lifetimes appropriate (DbContext = Scoped).

---

## What to include in GitHub repo

- All source projects and csproj files (Core, Data, Services, Infrastructure, Web)
- Migrations folder inside `ServiceApp.Data`
- README.md (this file)
- .gitignore (for .NET)
- Example `appsettings.json` (with placeholder values) — do NOT commit secrets
- LICENSE (choose a license)
- CONTRIBUTING.md (optional)
- Any CI/CD pipeline config (GitHub Actions, Azure Pipelines)

---

## License

Include a license file (e.g., MIT) depending on your preference.

---

## Contact

For questions or issues, open an issue in the repo or contact the maintainers.
