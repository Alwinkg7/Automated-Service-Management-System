# ASMS API reference

Host: **ServiceApp.Api**. Dev URLs are `https://localhost:7001` and `http://localhost:5001`. Swagger UI is at `/swagger` (Development only).

All routes below sit under the API origin. JSON property names are camelCase. Enum values are strings (`"Plumbing"`, `"Pending"`). Matching of enum query parameters ignores case.

---

## 1. Conventions

### Authentication

- Send `Authorization: Bearer <accessToken>` on every protected endpoint.
- Role-protected controllers return:
  - **401** when the token is missing, invalid, expired, or belongs to a deleted, deactivated or locked-out user.
  - **403** when the token is valid but the role is wrong, or the resource belongs to someone else.
- The API never redirects. You will not get a 302 to a login page.

### Success envelope (unchanged from the original Flutter contract)

```json
{ "success": true, "data": { ... } }
```

Some legacy endpoints put fields at the top level instead of under `data`. Examples are `requestId`, `billId`, `newStatus` and `isActive`. The shapes below show exactly what each endpoint returns.

### Error envelope: RFC 7807 ProblemDetails (`application/problem+json`)

Every error has this shape (validation, auth, business rules, rate limits, unhandled exceptions):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Invalid category.",
  "instance": "/api/customer/requests",
  "traceId": "0HN...:00000001",
  "success": false,
  "error": "Invalid category."
}
```

- `error` always holds a human-readable message. Existing clients use it. It is the first validation message when `errors` is present.
- Validation failures (400) also include `errors`, a map from field name to a list of messages:
  `"errors": { "Email": ["The Email field is not a valid e-mail address."] }`.

### Status codes

| Code | Meaning |
|---|---|
| 200 | OK |
| 201 | Created (registration) |
| 204 | No content (logout) |
| 400 | Invalid input or validation error |
| 401 | Not authenticated, bad credentials, or bad refresh token |
| 403 | Wrong role, or not the owner of the resource |
| 404 | Resource not found (also returned when a route id is not an integer) |
| 405 | Wrong HTTP method |
| 409 | Conflict: duplicate email, bill already paid, request in the wrong state, concurrent update |
| 410 | Endpoint removed (see `POST /api/customer/bills/{id}/pay`) |
| 415 | Wrong Content-Type (POST bodies must be `application/json`) |
| 422 | Business rule violated |
| 429 | Rate limited. See the `Retry-After` header (seconds) |
| 500 | Unexpected server error |
| 502 | Upstream (Razorpay/SendGrid) error |
| 503 | Database unavailable |
| 504 | Upstream timeout |

### Rate limits

| Policy | Applies to | Limit |
|---|---|---|
| `auth` | `/api/auth/login`, `/api/auth/refresh`, `/api/auth/logout`, `/api/auth/register/*` | 10 requests per minute per IP |
| `payments` | `/api/customer/bills/{id}/razorpay/*` | Token bucket per user: 10 burst, refill 5 per minute |
| `general-api` | every other controller endpoint | 120 requests per minute per user (or per IP when anonymous) |

The limits do not apply to `/hubs/service`, `/health`, `/hangfire`, `/api/auth/me` or `/api/payments/webhook`.

### Dates

- Fields named `*At` with ISO-8601 values (`expiresAt`, `paidAt`, `timestamp`) are UTC.
- Display fields such as `scheduledDate` and `createdAt` on request and job objects are **pre-formatted strings**, for example `"05 Nov 2026, 02:30 PM"` or `"05 Nov 2026"`. This is legacy behaviour.

---

## 2. Auth flow

1. `POST /api/auth/login` (or `register/*`) returns `token` (access JWT, short-lived) and `refreshToken` (opaque, single use).
2. Call APIs with `Authorization: Bearer <token>`.
3. When a call returns 401 (or shortly before `expiresAt`), call `POST /api/auth/refresh` with `{ "refreshToken": "..." }`. You get a **new pair**. The old refresh token is consumed, so store the new one.
4. A refresh that returns 401 means you must log in again.
5. To log out, call `POST /api/auth/logout` with the refresh token, then discard both tokens.

Access-token claims: `sub` (user id), `email`, `role` (`Admin` | `Customer` | `Technician`), `fullName` and `jti`.

> The default access-token lifetime is `Jwt:AccessTokenMinutes` = 10080 (7 days). This keeps the existing Flutter app working, because it does not refresh yet. Lower it (for example to 15–60) once every client refreshes. Refresh tokens last `Jwt:RefreshTokenDays` (30).

### Auth response (`AuthResponse`)

Returned by login, refresh and register:

```json
{
  "success": true,
  "token": "eyJhbGciOi...",
  "expiresAt": "2026-11-22T10:00:00Z",
  "refreshToken": "<userId>.<tokenId>.<secret>",
  "refreshTokenExpiresAt": "2026-12-15T10:00:00Z",
  "role": "Customer",
  "userId": "8f0c...-guid",
  "name": "Asha Rao",
  "email": "asha@example.com"
}
```

---

## 3. Endpoints

### Auth: `/api/auth`

| Method | Route | Auth | Body | Success |
|---|---|---|---|---|
| POST | `/api/auth/login` | anonymous | `{ "email": "a@b.com", "password": "Secret123" }` | 200 `AuthResponse` |
| POST | `/api/auth/refresh` | anonymous | `{ "refreshToken": "..." }` | 200 `AuthResponse` |
| POST | `/api/auth/logout` | anonymous | `{ "refreshToken": "..." }` | 204 |
| POST | `/api/auth/register/customer` | anonymous | `RegisterCustomer` | 201 `AuthResponse` |
| POST | `/api/auth/register/technician` | anonymous | `RegisterTechnician` | 201 `AuthResponse` |
| GET | `/api/auth/me` | any role | none | 200 `{ success, data: { userId, name, email, phone, role } }` |

Errors:

- **login**:
  - 400 validation
  - 401 `"Invalid credentials."` (also returned for inactive users)
  - 401 `"Too many failed attempts. Account locked for 5 minutes."` (5 failures lock the account for 5 minutes)
- **refresh**: 401 `"Invalid or expired refresh token."`
- **register**:
  - 400 validation (also returned for Identity password-rule failures)
  - 409 `"This email is already registered. Please login instead."`

```jsonc
// RegisterCustomer
{ "fullName": "Asha Rao",       // 2-100 chars
  "email": "asha@example.com",
  "phone": "9876543210",        // 10-15 chars
  "password": "Secret123",      // min 8, needs an uppercase letter and a digit
  "referralCode": "ASHA1234" }  // optional

// RegisterTechnician
{ "fullName": "Ravi K", "email": "ravi@example.com", "phone": "9876543210",
  "password": "Secret123",
  "skill": "Plumbing" }  // a ServiceCategory value
```

`ServiceCategory` values: `Plumbing`, `Electrical`, `Carpentry`, `Cleaning`, `AC_Repair`, `Appliance_Repair`, `Painting`, `General`.

`RequestStatus` values: `Pending`, `Assigned`, `InProgress`, `Billed`, `Completed`, `Cancelled`.

`TechnicianStatus` values: `Available`, `Busy`, `Offline`.

### Customer: `/api/customer` (role `Customer`)

| Method | Route | Body | Success |
|---|---|---|---|
| GET | `/dashboard` | none | `{ success, stats: { total, pending, inProgress, completed }, recentRequests: RequestSummary[] }` (the last 5) |
| GET | `/requests?status=Pending` | none | `{ success, data: RequestSummary[] }` (newest first; `status` is optional) |
| GET | `/requests/{id}` | none | `{ success, data: RequestDetail }`. 404 if missing, 403 if it belongs to someone else |
| GET | `/categories` | none (**anonymous allowed**) | `{ success, data: [{ value, label, description, icon }] }` |
| POST | `/requests` | `CreateRequest` | 200 `{ success, requestId }` |
| POST | `/requests/{id}/cancel` | none | 200 `{ success }`. Fails (400) unless the service allows cancelling in the current state |
| POST | `/requests/{id}/rate` | `{ "stars": 1-5, "comment": "optional" }` | 200 `{ success }` |
| POST | `/bills/{id}/razorpay/order` | none | 200 `{ success, data: { billId, orderId, amount, currency, keyId } }` |
| POST | `/bills/{id}/razorpay/verify` | `VerifyPayment` | 200 `{ success, data: { paymentId, billId, amount, method, transactionId, paidAt } }` |
| GET | `/bills/{id}/pdf` | none | 200 `application/pdf` (download `bill-{id}.pdf`) |
| POST | `/bills/{id}/pay` | none | **410 Gone**. Removed, because it marked bills paid without a payment. Use the Razorpay order and verify endpoints instead |

```jsonc
// CreateRequest
{ "category": "Plumbing",                 // ServiceCategory, case-insensitive
  "description": "Kitchen tap leaking",   // 5-1000 chars
  "scheduledDate": "2026-11-20T10:30:00", // must be in the future
  "address": "12 MG Road, Bengaluru",     // 5-300 chars
  "pinCode": "560001" }                   // optional

// VerifyPayment: the values Razorpay Checkout passes to its handler
{ "razorpayOrderId": "order_...", "razorpayPaymentId": "pay_...", "razorpaySignature": "hex" }

// RequestSummary
{ "id": 42, "category": "Plumbing", "status": "Pending", "description": "...",
  "scheduledDate": "20 Nov 2026", "createdAt": "15 Nov 2026",
  "technicianName": null, "hasBill": false, "billPaid": false, "billAmount": null }

// RequestDetail
{ "id": 42, "category": "Plumbing", "status": "Billed", "description": "...", "address": "...",
  "scheduledDate": "20 Nov 2026, 10:30 AM", "createdAt": "15 Nov 2026, 09:12 AM",
  "canCancel": false, "rating": null,
  "technician": { "name": "Ravi K", "phone": "...", "email": "...", "rating": 4.5 },   // or null
  "bill": { "id": 7, "amount": 850.00, "laborCost": 500.00, "materialCost": 350.00,
            "description": "...", "isPaid": false, "createdAt": "15 Nov 2026" },       // or null
  "statusHistory": [ { "status": "Pending", "changedAt": "15 Nov 2026, 09:12 AM", "note": "..." } ] }
```

**Payment flow (Razorpay Checkout):**

1. `POST /api/customer/bills/{billId}/razorpay/order`. The server sets the amount from the bill, in paise. The order id is stored against the bill.
2. Open Razorpay Checkout with `key: keyId`, `order_id: orderId`, `amount` and `currency`.
3. In the Checkout success handler, call `POST /api/customer/bills/{billId}/razorpay/verify` with the three `razorpay_*` values.
4. The server checks three things:
   - the HMAC signature;
   - that the order id equals the one stored on the bill;
   - that the bill is still unpaid.

   It then completes the request (status becomes `Completed`). The call is idempotent: repeating it with the same payment id returns the same payment.
5. Razorpay also calls `POST /api/payments/webhook`. Whichever call arrives first records the payment, and the other one becomes a no-op.

Errors:

- 404 for an unknown bill
- 403 when the bill is not yours
- 409 when the bill is already paid or the request is not `Billed`
- 400 for a bad signature or an order mismatch
- 502 when Razorpay is unavailable

### Technician: `/api/technician` (role `Technician`)

| Method | Route | Body | Success |
|---|---|---|---|
| GET | `/dashboard` | none | `{ success, technician: { name, status, rating, totalJobs, completedJobs, pendingBills }, activeJob: {...} \| null, recentJobs: [{ id, category, customerName, completedAt, billPaid }] }` |
| POST | `/toggle-status` | none | `{ success, newStatus: "Available" \| "Offline" }`. 400 while `Busy` |
| GET | `/jobs?status=Assigned` | none | `{ success, data: [{ id, category, status, description, address, scheduledDate, customerName, customerPhone, hasBill, billPaid }] }` |
| GET | `/jobs/{id}` | none | `{ success, data: { id, category, status, description, address, scheduledDate, createdAt, canAccept, canBill, canComplete, customer: { name, phone, email }, bill: { amount, laborCost, materialCost, description, isPaid } \| null } }`. 403 if the job is not yours |
| POST | `/jobs/{id}/accept` | none | `{ success }` (`Assigned` becomes `InProgress`) |
| POST | `/jobs/{id}/reject` | none | `{ success }` (`Assigned` becomes `Pending`, ready to be reassigned) |
| POST | `/jobs/{id}/bill` | `{ "laborCost": 500, "materialCost": 350, "description": "optional" }` | `{ success, billId }` (`InProgress` becomes `Billed`). At least one cost must be greater than 0 |
| POST | `/jobs/{id}/complete` | none | `{ success, data: { paymentId } }`. The technician confirms **cash collected**. This records a Cash payment and moves `Billed` to `Completed`. 403 if the job is not yours; 409 if it is not `Billed` or is already paid |

### Admin: `/api/admin` (role `Admin`)

| Method | Route | Body | Success |
|---|---|---|---|
| GET | `/dashboard` | none | `{ success, stats: { totalRequests, pending, inProgress, completed, totalCustomers, totalTechnicians, revenue }, pendingRequests: [{ id, category, status, customerName, scheduledDate, createdAt, address }] }` |
| GET | `/requests?status=` | none | `{ success, data: [{ id, category, status, customerName, technicianName, scheduledDate, createdAt, hasBill, billPaid, billAmount, isUnassigned }] }` |
| GET | `/requests/{id}` | none | `{ success, data: { id, category, status, description, address, scheduledDate, createdAt, canAssign, customer: { name, email, phone }, technician: { id, name, phone, rating, status } \| null, bill: { amount, isPaid } \| null } }` |
| GET | `/technicians/available?category=Plumbing` | none | `{ success, data: [{ id, name, phone, rating, totalJobs, skills, status }] }` (active technicians only) |
| POST | `/requests/{id}/assign` | `{ "technicianProfileId": 3 }` | `{ success }` |
| GET | `/users?role=Customer` | none | `{ success, data: [{ id, name, email, phone, role, isActive, createdAt }] }` |
| POST | `/users/{id}/toggle-active` | none | `{ success, isActive }`. 409 when you target yourself or the last active admin |

### Payments webhook

| Method | Route | Auth | Notes |
|---|---|---|---|
| POST | `/api/payments/webhook` | `X-Razorpay-Signature` header (HMAC-SHA256 of the raw body with `Razorpay:WebhookSecret`) | Handles `order.paid` and `payment.captured`. Returns 200 `{ success, handled }`, or 401 for a bad signature. Configure it in the Razorpay dashboard and subscribe to `order.paid`. |

### Operational

| Method | Route | Auth | Notes |
|---|---|---|---|
| GET | `/health` | anonymous | Returns `Healthy` or `Unhealthy` (includes a database check) |
| GET | `/swagger` | anonymous | Development only |
| GET | `/hangfire` | Admin JWT, or the MVC admin cookie (same domain) | Hangfire dashboard |

---

## 4. SignalR (realtime)

- Hub URL: `{API origin}/hubs/service`.
- Authentication: pass the access token through `accessTokenFactory`. The JS client sends it as `?access_token=` for WebSockets and SSE.

```ts
import { HubConnectionBuilder } from "@microsoft/signalr";
const connection = new HubConnectionBuilder()
  .withUrl(`${API}/hubs/service`, { accessTokenFactory: () => getAccessToken() })
  .withAutomaticReconnect()
  .build();
connection.on("JobAssigned", (p) => { /* ... */ });
await connection.start();
```

**Groups are assigned on the server when you connect**, based on the token's claims:

- `customer-{userId}`
- `technician-{userId}`, plus `technicians-available`
- `admin-{userId}`, plus `admin`

You do not need to call anything to join. `JoinGroup(role, userId)` still exists for older clients, but it ignores its arguments.

### Client to server methods

| Method | Args | Who | Effect |
|---|---|---|---|
| `JoinGroup` | `(string role, string userId)` | any | No-op that re-joins your own groups (legacy) |
| `UpdateAvailability` | `(bool isAvailable)` | Technician | Joins or leaves `technicians-available` |
| `ShareLocation` | `(int requestId, double lat, double lng, string customerId)` | Technician | Sends `LocationUpdate` to the request's customer. You must be the assigned technician and the request must be `InProgress`. `customerId` is ignored; pass `""` |
| `StopSharing` | `(int requestId, string customerId)` | Technician | Sends `LocationStopped` to the request's customer. You must be the assigned technician. `customerId` is ignored |

### Server to client events

| Event | Target | Payload |
|---|---|---|
| `JobAssigned` | assigned technician | `{ requestId, category, customerName, address, message, timestamp }` |
| `NewJobAvailable` | `technicians-available` | `{ requestId, category, message, timestamp }` |
| `JobAccepted` | customer | `{ requestId, technicianName, technicianPhone, message, timestamp }` |
| `BillCreated` | customer | `{ requestId, totalAmount, message, timestamp }` |
| `JobCompleted` | technician | `{ requestId, amountEarned, message, timestamp }` |
| `NewRequest` | `admin` | `{ requestId, category, customerName, message, timestamp }` |
| `StatusChanged` | `admin` | `{ requestId, newStatus, message, timestamp }` |
| `LocationUpdate` | customer | `{ requestId, lat, lng, timestamp }` |
| `LocationStopped` | customer | `{ requestId }` |

> **Cross-host delivery.** The MVC site (ServiceApp.Web) runs its own copy of the same hub.
>
> - **Without** `SignalR:RedisConnectionString`, an event reaches only the clients connected to the host that raised it. For example, an event raised by an MVC admin action reaches MVC browsers only. Hangfire auto-assign runs in the API, so its events reach API clients only.
> - **With** Redis configured on both hosts, every event reaches every client.

---

## 5. Contract changes versus the old ServiceApp.Web `/api`

All existing routes and success shapes are preserved, with these exceptions:

1. Error bodies are now ProblemDetails (`application/problem+json`). They still include `success: false` and `error`.
2. `POST /api/customer/bills/{id}/pay` now returns **410 Gone**. It used to mark the bill paid without any payment (QA BUG-02).
3. `POST /api/technician/jobs/{id}/complete` used to be a no-op stub. It now confirms cash collection and completes the job (BUG-07). It returns 403 or 409 when invalid.
4. Login locks the account after 5 failures and is rate-limited. Deactivated users' tokens are rejected (401) within about 30 seconds.
5. Route ids are now constrained to `int`, so a non-numeric id returns 404 instead of 400.
6. In Development, CORS allows only `Cors:AllowedOrigins` plus loopback origins. It used to allow every origin.
7. New (additive) endpoints:
   - refresh, logout, register and me
   - Razorpay order and verify
   - bill PDF
   - job reject
   - payment webhook
   - `/health`
8. New fields on the login response: `expiresAt`, `refreshToken` and `refreshTokenExpiresAt`.
