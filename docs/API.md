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
| global | every `/api/*` request, in addition to the policies above | 120 requests per minute per user (or per IP when anonymous) |

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
| GET | `/requests?status=Pending` | none | `{ success, data: RequestSummary[] }` (newest first; `status` is optional; filtered in SQL) |
| GET | `/requests/{id}` | none | `{ success, data: RequestDetail }`. 404 if missing, 403 if it belongs to someone else. `RequestDetail` also has `pinCode`, `feedback`, `canRebook`, and `bill.{ paidAt, discount, payable, promoCode, pointsRedeemed, preferredPaymentMethod, paymentMethod, items: [{ description, quantity, unitPrice, amount }] }` |
| GET | `/categories` | none (**anonymous allowed**) | `{ success, data: [{ value, label, description, icon }] }` |
| POST | `/requests` | `CreateRequest` | 200 `{ success, requestId }` |
| POST | `/requests/{id}/cancel` | none | 200 `{ success }`. Allowed while `Pending` or `Assigned` (the same rule as `canCancel`), otherwise 400 |
| POST | `/requests/{id}/rate` | `{ "stars": 1-5, "comment": "optional" }` | 200 `{ success }` |
| POST | `/bills/{id}/razorpay/order` | optional `{ "promoCode"?: string, "redeemPoints"?: boolean }`; honours `Idempotency-Key` | 200 `{ success, data: { billId, orderId, amount, currency, keyId, subtotal, discount, payable, promoCode, pointsRedeemed } }`. `amount` is the **discounted** payable amount in paise |
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
| POST | `/toggle-status` | none | `{ success, newStatus: "Available" \| "Offline" }`. 409 while `Busy` |
| GET | `/jobs?status=Assigned` | none | `{ success, data: [{ id, category, status, description, address, scheduledDate, customerName, customerPhone, hasBill, billPaid }] }` |
| GET | `/jobs/{id}` | none | `{ success, data: { id, category, status, description, address, pinCode, scheduledDate, createdAt, canAccept, canBill, canComplete, canShareLocation, customer: { name, phone, email }, bill: { id, amount, payable, discount, laborCost, materialCost, description, isPaid, paidAt, preferredPaymentMethod, items } | null } }`. 403 if the job is not yours |
| POST | `/jobs/{id}/accept` | none | `{ success }` (`Assigned` becomes `InProgress`) |
| POST | `/jobs/{id}/reject` | none | `{ success }` (`Assigned` becomes `Pending`, ready to be reassigned) |
| POST | `/jobs/{id}/bill` | `{ "laborCost": 500, "materialCost": 350, "description": "optional" }` | `{ success, billId }` (`InProgress` becomes `Billed`). At least one cost must be greater than 0 |
| POST | `/jobs/{id}/complete` | none | `{ success, data: { paymentId } }`. The technician confirms **cash collected**. This records a Cash payment and moves `Billed` to `Completed`. 403 if the job is not yours; 409 if it is not `Billed` or is already paid |

### Admin: `/api/admin` (role `Admin`)

| Method | Route | Body | Success |
|---|---|---|---|
| GET | `/dashboard` | none | `{ success, stats: { totalRequests, pending, inProgress, completed, totalCustomers, totalTechnicians, revenue }, pendingRequests: [{ id, category, status, customerName, scheduledDate, createdAt, address }] }` |
| GET | `/requests?status=&search=&from=&to=&page=&pageSize=` | none | Without `page`/`pageSize`: `{ success, data: AdminRequestRow[] }` (legacy shape). With either one: `{ success, data: { items: AdminRequestRow[], page, pageSize, totalCount } }` (pageSize max 100). Filtering runs in SQL; `from`/`to` are `yyyy-mm-dd` (inclusive, on createdAt) |
| GET | `/requests/export?status=&search=&from=&to=` | none | `text/csv` attachment, UTF-8 with BOM, same filters, formula injection escaped |
| GET | `/requests/{id}` | none | `{ success, data: AdminRequestDetail }` |
| POST | `/requests/{id}/cancel` | none | `{ success }`. Allowed while `Pending` or `Assigned`; 409 otherwise |
| POST | `/auto-assign` | none | `{ success, data: { assigned, stillPending } }`. Runs one auto-assignment cycle now |
| GET | `/technicians/available?category=Plumbing` | none | `{ success, data: [{ id, name, phone, rating, totalJobs, skills, status }] }` (active technicians only) |
| POST | `/requests/{id}/assign` | `{ "technicianProfileId": 3 }` | `{ success }` |
| GET | `/users?role=Customer` | none | `{ success, data: [{ id, name, email, phone, role, isActive, createdAt }] }` |
| POST | `/users/{id}/toggle-active` | none | `{ success, isActive }`. 409 when you target yourself or the last active admin |

#### Admin request shapes

```jsonc
// AdminRequestRow
{ "id": 42, "category": "Plumbing", "status": "Completed", "customerName": "Asha", "technicianName": "Ravi",
  "address": "12 MG Road", "pinCode": "682001", "scheduledDate": "20 Nov 2026", "createdAt": "15 Nov 2026",
  "createdAtUtc": "2026-11-15T09:12:00Z", "hasBill": true, "billPaid": true, "billAmount": 700.00,
  "paidAt": "2026-11-20T12:00:00Z", "isUnassigned": false }

// AdminRequestDetail
{ "id": 42, "category": "Plumbing", "status": "Billed", "description": "...", "address": "...", "pinCode": "682001",
  "scheduledDate": "20 Nov 2026, 10:30 AM", "createdAt": "15 Nov 2026",
  "canAssign": false, "canCancel": false, "feedback": null, "rating": null,
  "statusHistory": [ { "status": "Pending", "changedAt": "15 Nov 2026, 09:12 AM", "changedAtUtc": "2026-11-15T09:12:00Z", "note": "..." } ],
  "customer": { "name": "...", "email": "...", "phone": "..." },
  "technician": { "id": 3, "name": "...", "phone": "...", "rating": 4.5, "status": "Busy" },          // or null
  "bill": { "id": 7, "amount": 700.00, "laborCost": 500.00, "materialCost": 200.00, "discount": 50.00,
            "payable": 650.00, "paidAt": null, "paymentMethod": null,
            "items": [ { "description": "Labour charges", "quantity": 1, "unitPrice": 500.00, "amount": 500.00 } ],
            "isPaid": false } }                                                                    // or null
```

### Idempotency-Key

`POST /api/customer/requests`, `POST /api/customer/requests/{id}/rebook` and `POST /api/customer/bills/{id}/razorpay/order` accept an optional `Idempotency-Key: <uuid>` header (at most 100 characters).

- A successful (2xx) response is stored for **24 hours** per user + route + key.
- Repeating the key returns the stored response unchanged (same `requestId` / `orderId`) and adds the header `Idempotent-Replayed: true`.
- Concurrent requests with the same key are serialised, so a double click cannot create two records.
- Error responses are **not** stored, so a corrected retry with the same key runs normally.
- Storage is in-memory on the API instance (not shared between instances; lost on restart). Behind a load balancer, move it to Redis.

### Customer: disputes, promotions, discounts, cash, history (role `Customer`)

| Method | Route | Body | Success |
|---|---|---|---|
| GET | `/api/customer/disputes` | none | `{ success, data: Dispute[] }` own disputes, newest first |
| GET | `/api/customer/disputes/{id}` | none | `{ success, data: Dispute }`. 403 not yours, 404 missing |
| POST | `/api/customer/disputes` | `{ requestId, reason, description }` | 201 `{ success, data: { id } }`. Errors: 400 reason not in list or description not 20-2000 chars; 403 not your request; 409 a dispute is already open; 422 request not `Completed` |
| GET | `/api/customer/promotions` | none | `{ success, data: { pointsBalance, pointsValue, referralCode, history: [{ points, transactionType, note, requestId, transactedAt, expiresAt }] } }` |
| POST | `/api/customer/promotions/validate` | `{ code, billAmount }` | `{ success, data: { discount, message } }`. 400 invalid/expired/min amount/first-booking-only; 409 already used |
| GET | `/api/customer/bills/{id}/quote` | none | `{ success, data: Quote }` |
| POST | `/api/customer/bills/{id}/promo/preview` | `{ code }` | `{ success, data: Quote }` with the discount the code would give; nothing is recorded |
| POST | `/api/customer/bills/{id}/promo` | `{ code }` | `{ success, data: Quote }`. Applies the code to the bill (one code per bill: 409 if a code is already applied) |
| POST | `/api/customer/bills/{id}/points` | `{ points }` | `{ success, data: Quote }`. Multiples of 100 (100 points = ₹10); 422 when the balance is too low or the value exceeds the payable amount minus ₹1 |
| POST | `/api/customer/bills/{id}/cash` | none | `{ success, data: { billId, preferredPaymentMethod: "Cash", payable, message } }`. Records the choice only; the technician completes the job with `POST /api/technician/jobs/{id}/complete` |
| GET | `/api/customer/history?search=&category=` | none | `{ success, data: { totalJobs, completedJobs, cancelledJobs, totalSpent, averageRating, items: RequestSummary[] } }` (Completed and Cancelled only; filters in SQL) |
| POST | `/api/customer/requests/{id}/rebook` | `{ scheduledDate, description?, address? }` | 201 `{ success, requestId }`. Only own `Completed`/`Cancelled` requests (403/409); `scheduledDate` at least 1 hour ahead (400). Honours `Idempotency-Key` |

```jsonc
// Dispute
{ "id": 5, "requestId": 42, "reason": "ServiceQuality", "description": "...",
  "status": "Open" /* | "UnderReview" | "Resolved" */, "resolution": null /* | "Refunded" | "Rejected" | "ClosedNoAction" */,
  "adminNote": null, "refundAmount": null, "refundIssued": false, "razorpayRefundId": null,
  "raisedAt": "2026-11-20T12:00:00Z", "resolvedAt": null, "slaDeadline": "2026-11-22T12:00:00Z", "isSlaBreached": false,
  "raisedByName": "Asha", "category": "Plumbing", "billAmount": 700.00 }
// reason is one of: ServiceQuality, LateArrival, WrongCharge, Unprofessional, JobIncomplete, Other

// Quote (bill price breakdown)
{ "billId": 7, "subtotal": 700.00, "discount": 50.00, "payable": 650.00, "promoCode": "SAVE50", "pointsRedeemed": 0 }
```

**Discounts and payment.** Discounts are stored on the bill. `payable = subtotal - discount` (never below ₹1). The Razorpay order, the checkout verification, the webhook amount check and the technician cash confirmation all use `payable`. Applying a discount after an order exists invalidates that order: call `razorpay/order` again. The technician earnings and commission are calculated on the subtotal, so the platform absorbs the discount.

`razorpay/order` with `{ promoCode, redeemPoints: true }` applies the code (skipped if that code is already on the bill) and redeems the largest valid number of points once per bill, then creates the order for the discounted amount.

### Technician: wallet, location, bill PDF (role `Technician`)

| Method | Route | Body | Success |
|---|---|---|---|
| GET | `/api/technician/wallet` | none | `{ success, data: WalletSummary }` |
| GET | `/api/technician/wallet/earnings` | none | `{ success, data: Payout[] }` newest first |
| GET | `/api/technician/wallet/withdrawals` | none | `{ success, data: Withdrawal[] }` |
| POST | `/api/technician/wallet/withdrawals` | `{ amount, upiId }` | 201 `{ success, data: { id, withdrawal: Withdrawal } }`. 400 amount <= 0 or bad UPI; 422 amount > `availableToWithdraw` (computed on the server) |
| POST | `/api/technician/jobs/{id}/location` | `{ lat, lng }` | `{ success }`. Pushes `LocationUpdate` to the customer. 403 not your job; 409 job not `InProgress`. (The hub `ShareLocation` method does the same) |
| GET | `/api/technician/bills/{id}/pdf` | none | `application/pdf`. Only the technician who created the bill (403 otherwise) |

```jsonc
// WalletSummary
{ "grossEarningsAllTime": 700.00, "commissionAllTime": 84.00, "netEarningsAllTime": 616.00,
  "grossEarningsThisMonth": 700.00, "netEarningsThisMonth": 616.00, "grossEarningsThisWeek": 0, "netEarningsThisWeek": 0,
  "availableToWithdraw": 516.00, "pendingWithdrawal": 0, "totalWithdrawn": 100.00,
  "totalJobsCompleted": 1, "averagePerJob": 616.00, "commissionRate": 0.12,
  "monthly": [ { "label": "Nov 26", "gross": 700.00, "net": 616.00 } ],      // last 6 months, oldest first
  "daily":   [ { "label": "15 Nov", "gross": 0 } ],                          // last 30 days
  "payouts": [ Payout ], "withdrawals": [ Withdrawal ] }

// Payout
{ "requestId": 42, "billId": 7, "customerName": "Asha", "category": "Plumbing", "grossAmount": 700.00,
  "commission": 84.00, "netAmount": 616.00, "paymentMethod": "Cash", "paidAt": "2026-11-20T12:00:00Z" }

// Withdrawal
{ "id": 3, "amount": 100.00, "status": "Pending" /* | "Approved" | "Paid" | "Rejected" */, "upiId": "tech@upi",
  "adminNote": null, "requestedAt": "2026-11-20T12:00:00Z", "processedAt": null, "technicianName": "Ravi" }
```

### Admin: disputes, withdrawals, zones, promo codes, technicians, analytics (role `Admin`)

| Method | Route | Body | Success |
|---|---|---|---|
| GET | `/api/admin/disputes?status=Open\|UnderReview\|Resolved` | none | `{ success, data: Dispute[], counts: { Open, UnderReview, Resolved, SlaBreached } }` |
| GET | `/api/admin/disputes/{id}` | none | `{ success, data: Dispute }` |
| POST | `/api/admin/disputes/{id}/review` | `{ adminNote? }` (body optional) | `{ success, data: Dispute }`. 409 if not `Open` |
| POST | `/api/admin/disputes/{id}/resolve` | `{ resolution, adminNote? }` | `{ success, data: Dispute }`. `resolution` is `Rejected` or `ClosedNoAction` (400 otherwise); 409 if already resolved |
| POST | `/api/admin/disputes/{id}/refund` | `{ amount, adminNote? }` | `{ success, data: { razorpayRefundId, dispute } }`. 400 amount <= 0 or > amount paid; 409 already refunded; **422 cash-paid request** (refunds are online only, as in MVC); 502 Razorpay error |
| GET | `/api/admin/withdrawals?status=` | none | `{ success, data: Withdrawal[] }` newest first. `status` = `Pending`, `Approved`, `Paid`, `Rejected`, or `open` (Pending + Approved) |
| POST | `/api/admin/withdrawals/{id}/approve` | none | `{ success, data: Withdrawal }`. 409 if not `Pending` |
| POST | `/api/admin/withdrawals/{id}/pay` | none | `{ success, data: Withdrawal }`. 409 if not `Approved` |
| POST | `/api/admin/withdrawals/{id}/reject` | `{ adminNote? }` (body optional) | `{ success, data: Withdrawal }`. 409 if already `Paid` or `Rejected` |
| GET | `/api/admin/zones` | none | `{ success, data: Zone[] }` (active and inactive) |
| GET | `/api/admin/zones/{id}` | none | `{ success, data: Zone }` |
| POST | `/api/admin/zones` | `{ zoneName, city, state, pinCodes: "682001,682002", isActive? }` | 201 `{ success, data: Zone }`. 400 pin codes not 6 digits; 409 duplicate name or a pin code already in another active zone |
| PUT | `/api/admin/zones/{id}` | same body | `{ success, data: Zone }` |
| POST | `/api/admin/zones/{id}/toggle` | none | `{ success, data: Zone }` (zones are deactivated, never deleted) |
| GET | `/api/admin/promo-codes` | none | `{ success, data: PromoCode[] }` newest first |
| GET | `/api/admin/promo-codes/{id}` | none | `{ success, data: PromoCode }` |
| POST | `/api/admin/promo-codes` | `{ code, description?, discountType: Flat\|Percentage\|FirstOnly, discountValue, maxDiscount?, minimumAmount, maxUses?, expiresAt?, isActive? }` | 201 `{ success, data: PromoCode }`. 409 code exists; 400 invalid (percentage > 100, expiry in the past, code not 3-30 letters/digits) |
| PUT | `/api/admin/promo-codes/{id}` | same body | `{ success, data: PromoCode }`. A used code cannot be renamed (409) |
| POST | `/api/admin/promo-codes/{id}/toggle` | none | `{ success, data: PromoCode }` |
| DELETE | `/api/admin/promo-codes/{id}` | none | 204. 409 if the code was used (deactivate it instead) |
| GET | `/api/admin/technicians?skill=&status=&search=&isActive=&page=&pageSize=` | none | `{ success, data: Technician[], counts }` or, with `page`/`pageSize`, `{ success, data: { items, page, pageSize, totalCount }, counts }`. `counts = { total, available, busy, offline }` |
| GET | `/api/admin/technicians/{id}` | none | `{ success, data: { technician: Technician, monthlyEarnings: [{ label, amount }], recentJobs: [{ id, category, status, customerName, createdAt, billAmount, billPaid }] } }` |
| POST | `/api/admin/technicians/{id}/status` | `{ status: Available\|Busy\|Offline }` | `{ success, status }` |
| POST | `/api/admin/technicians/{id}/skill` | `{ skill }` | `{ success, skill }`. 409 while the technician has an Assigned/InProgress/Billed job |
| POST | `/api/admin/technicians/{id}/activate` | none | `{ success, isActive }` |
| POST | `/api/admin/technicians/{id}/deactivate` | none | `{ success, isActive }`. Sessions end within about a minute |
| GET | `/api/admin/analytics` | none | `{ success, data: { totals: { totalRequests, completedRequests, pendingRequests, totalCustomers, totalTechnicians, totalRevenue, completionRate }, statusBreakdown: { Pending, Assigned, ... }, revenueByDay: [{ label, amount }], requestsByDay: [{ label, count }], categories: [{ category, count }], topTechnicians: Technician[], recentActivity: [{ requestId, status, note, changedAt, category }] } }`. Revenue is grouped by bill **paidAt** (last 7 days) |

```jsonc
// Zone
{ "id": 1, "zoneName": "Kochi", "city": "Kochi", "state": "Kerala", "isActive": true,
  "pinCodes": ["682001", "682002"], "technicianCount": 4, "createdAt": "2026-11-15T09:00:00Z" }

// PromoCode
{ "id": 2, "code": "SAVE50", "description": "...", "discountType": "Flat", "discountValue": 50, "maxDiscount": null,
  "minimumAmount": 100, "maxUses": null, "usedCount": 3, "isActive": true, "isValid": true,
  "expiresAt": null, "createdAt": "2026-11-15T09:00:00Z" }

// Technician
{ "id": 3, "userId": "...", "name": "Ravi", "email": "...", "phone": "...", "skill": "Plumbing", "status": "Available",
  "rating": 4.5, "totalJobsCompleted": 12, "isActive": true, "zoneId": 1, "zoneName": "Kochi",
  "totalEarned": 18400.00, "activeJobCount": 0, "bio": null, "yearsOfExperience": 5, "serviceAreaPinCode": "682001" }
```

### Frontend alignment (differences from `frontend/docs/api-requests.md`)

The requested routes and shapes are implemented as proposed, except:

1. **Dispute reasons** also accept `JobIncomplete`, because the MVC form offers it.
2. **Withdrawal create** returns `{ id, withdrawal }` inside `data` (the full Withdrawal is extra).
3. **Zones**: `pinCodes` in responses is always a `string[]`. Requests send a comma, space or newline separated string.
4. **Admin requests export** is at `/api/admin/requests/export` (as requested); MVC keeps `/Admin/Requests/ExportCsv`.
5. **Paged admin lists** (`/requests`, `/technicians`) return the paged shape only when `page` or `pageSize` is sent; otherwise the legacy array (kept for the Flutter app).
6. **Razorpay order** response adds `subtotal`, `payable`, `promoCode` and `pointsRedeemed` next to `discount`. `amount` stays in **paise** (discounted).
7. **Location**: `POST /api/technician/jobs/{id}/location` was added for parity although the frontend uses the hub.
8. **Analytics** wraps totals under `totals` and uses `statusBreakdown`, `revenueByDay`, `requestsByDay`, `categories`, `topTechnicians`, `recentActivity`.
9. **`/promotions/validate`** errors: an unknown code is 400 (not 404); already used is 409.

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

Round 2 (parity with MVC):

9. **More precise error codes.** Service failures now carry a type, so some errors that used to be 400 are now:
   - 404 when the request, bill, technician or dispute does not exist;
   - 403 when the resource is not yours (for example accepting or rejecting a job assigned to someone else);
   - 409 for a wrong state (accepting a non-Assigned job, cancelling after work started, rating twice, billing twice);
   - 422 for business rules (insufficient wallet balance, dispute on a request that is not Completed, refund of a cash payment).
   Clients that only check `success`/`error` are unaffected.
10. **Cash is confirmed by the technician only.** The MVC "Pay with cash" button and `POST /api/customer/bills/{id}/cash` only record the choice; `POST /api/technician/jobs/{id}/complete` (or the MVC "Confirm cash received" button) completes the job.
11. **Discounts change the charged amount.** Razorpay orders, payment verification and webhooks use the bill's `payable` amount (subtotal minus promo/points discount). Bills without a discount behave as before.
12. **Disputes**: `reason` must be one of the listed codes and `description` is 20-2000 characters (the MVC form uses the same rules).
13. **Zones**: pin codes must be 6 digits and may belong to only one active zone.
14. **Admin dashboard** `totalCustomers` now counts active customers only.
