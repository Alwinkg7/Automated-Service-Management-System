# API requests from the frontend

Endpoints the Next.js frontend calls that are **not yet in `docs/API.md`**. The screens are built and typed against these shapes (zod schemas in `src/lib/api/schemas.ts`, calls in `src/lib/api/endpoints/*`). Until an endpoint exists, its screen shows a "Not available yet" state.

If the backend picks a different shape, update `docs/API.md`. The frontend then changes the schema and endpoint function only.

Conventions are the ones in `docs/API.md`: the `{ success, data }` envelope, ProblemDetails errors, camelCase, string enums. `*At` fields are ISO-8601 UTC.

---

## Cross-cutting

### Idempotency-Key on booking (double-booking guard)

`POST /api/customer/requests` and `POST /api/customer/bills/{id}/razorpay/order` already send an `Idempotency-Key: <uuid>` header. The key is generated once per form attempt and reused on retry or double click.
Request: when the same user repeats a key within 24h, return the **original** response (the same `requestId`) instead of creating a second request.

### Server-side paging + filters for admin requests (scale)

`GET /api/admin/requests?search=&status=&from=yyyy-mm-dd&to=yyyy-mm-dd&page=1&pageSize=20`
Response: `{ success, data: { items: AdminRequestRow[], page, pageSize, totalCount } }`. Add `address` and `pinCode` to the row.
The frontend accepts **either** this paged shape or the current array (`pagedOrArray` in schemas.ts). It filters client-side today.

### CSV export

`GET /api/admin/requests/export?search=&status=&from=&to=` returns `text/csv` with `Content-Disposition: attachment`. This is optional, because the client generates CSV today.

### Admin request cancel and manual auto-assign

- `POST /api/admin/requests/{id}/cancel` → `{ success }`. 409 if Completed or Cancelled.
- `POST /api/admin/auto-assign` → `{ success, data: { assigned: number } }`.

### Detail fields

- Customer `GET /requests/{id}`: add `pinCode` and `feedback`. Bill: add `items: [{ description, quantity, unitPrice, amount }]` and `paidAt`.
- Technician `GET /jobs/{id}`: add `pinCode`, and `bill.id`, so the technician can download the PDF.
- Admin `GET /requests/{id}`: add `pinCode`, `statusHistory: [{ status, changedAt, note }]` and `canCancel`. Bill: add `id`, `laborCost` and `materialCost`.
- Technician bill PDF: `GET /api/technician/bills/{id}/pdf` (owner technician only).

---

## Customer

### GET /api/customer/promotions

```
200 { success, data: {
  pointsBalance: number, pointsValue: number,     // ₹ value of redeemable points
  referralCode: string,
  history: [{ points, transactionType, note, requestId, transactedAt, expiresAt }] } }
```

### POST /api/customer/promotions/validate

```
Request:  { code: string, billAmount: number }
Response: 200 { success, data: { discount: number, message?: string } }
Errors:   400 invalid/expired/min-amount · 409 already used
```

### POST /api/customer/bills/{id}/razorpay/order (extend the existing endpoint)

Accept an optional body `{ promoCode?: string, redeemPoints?: boolean }`. Apply the discounts on the server. Return `discount` alongside the existing fields, with `amount` already discounted. The frontend sends this body today. The current endpoint ignores it.

### Disputes

```
GET  /api/customer/disputes        → 200 { success, data: Dispute[] }        (own disputes, newest first)
GET  /api/customer/disputes/{id}   → 200 { success, data: Dispute }          403 not owner · 404
POST /api/customer/disputes        { requestId, reason, description }
                                   → 201 { success, data: { id } }
     Errors: 400 (description < 20 chars, reason not in list) · 403 not owner
             · 409 dispute already open · 422 request not Completed

Dispute = { id, requestId, reason, description,
            status: "Open"|"UnderReview"|"Resolved",
            resolution?: "Refunded"|"Rejected"|"ClosedNoAction", adminNote?,
            refundAmount?, refundIssued?, razorpayRefundId?,
            raisedAt, resolvedAt?, slaDeadline, isSlaBreached,
            raisedByName?, category?, billAmount? }
reason ∈ ServiceQuality | LateArrival | WrongCharge | Unprofessional | Other
```

## Technician

### GET /api/technician/wallet

```
200 { success, data: {
  grossEarningsAllTime, commissionAllTime, netEarningsAllTime,
  netEarningsThisMonth, netEarningsThisWeek,
  availableToWithdraw, pendingWithdrawal,
  totalJobsCompleted, averagePerJob, commissionRate,            // 0.12
  monthly:  [{ label: "Nov 26", gross, net }],                  // last 6 months
  payouts:  [{ requestId, billId, customerName, category, grossAmount, commission, netAmount, paymentMethod, paidAt }],
  withdrawals: Withdrawal[] } }
Withdrawal = { id, amount, status: "Pending"|"Approved"|"Paid"|"Rejected", upiId, adminNote?, requestedAt, processedAt?, technicianName? }
```

### POST /api/technician/wallet/withdrawals

```
Request:  { amount: number, upiId: string }
Response: 201 { success, data: { id } }
Errors:   400 amount <= 0 / bad UPI · 422 amount > availableToWithdraw
```

Location: the frontend uses the hub `ShareLocation` and `StopSharing` methods (documented). **No REST endpoint is needed.**

## Admin

### Disputes and refunds

```
GET  /api/admin/disputes?status=Open|UnderReview|Resolved → { success, data: Dispute[] }
GET  /api/admin/disputes/{id}                             → { success, data: Dispute }
POST /api/admin/disputes/{id}/review   { adminNote }                → { success }      409 if not Open
POST /api/admin/disputes/{id}/resolve  { resolution, adminNote }    → { success }      resolution ∈ Rejected|ClosedNoAction
POST /api/admin/disputes/{id}/refund   { amount, adminNote }        → { success, data: { razorpayRefundId } }
     Errors: 400 amount <= 0 or > bill total · 409 already refunded/resolved · 502 Razorpay
```

### Withdrawals

```
GET  /api/admin/withdrawals               → { success, data: Withdrawal[] }   (includes technicianName)
POST /api/admin/withdrawals/{id}/approve  → { success }   409 if not Pending
POST /api/admin/withdrawals/{id}/pay      → { success }   409 if not Approved
POST /api/admin/withdrawals/{id}/reject   { adminNote }   → { success }
```

### Zones

```
GET  /api/admin/zones            → { success, data: [{ id, zoneName, city, state, isActive, pinCodes: string[] | "csv", technicianCount? }] }
POST /api/admin/zones            { zoneName, city, state, pinCodes: "680001,680002", isActive } → 201
PUT  /api/admin/zones/{id}       same body → 200
POST /api/admin/zones/{id}/toggle → { success }
Errors: 400 validation (6-digit pin codes) · 409 duplicate zone name / pin code already in another zone
```

### Promo codes

```
GET  /api/admin/promo-codes      → { success, data: [{ id, code, description, discountType, discountValue, maxDiscount?, minimumAmount, maxUses?, usedCount, isActive, expiresAt?, createdAt }] }
POST /api/admin/promo-codes      { code, description, discountType: Flat|Percentage|FirstOnly, discountValue, maxDiscount?, minimumAmount, maxUses?, expiresAt?: "yyyy-mm-dd", isActive } → 201
POST /api/admin/promo-codes/{id}/toggle → { success }
Errors: 409 code exists · 400 percentage > 100
```

### Technicians (full management)

```
GET  /api/admin/technicians?skill=&status= → { success, data: [{ id, userId, name, email, phone, skill, status, rating, totalJobsCompleted, isActive, zoneName?, totalEarned? }] }
POST /api/admin/technicians/{id}/status { status: Available|Busy|Offline } → { success }
```

The UI today combines `/users?role=Technician` with `/technicians/available`. Once this endpoint exists, it switches to this one.

### Analytics (optional, for efficiency)

`GET /api/admin/analytics` returns totals, a status breakdown, revenue by day based on **paidAt**, requests by day, categories, top technicians and recent activity. The UI computes most of this from `/requests` today. Revenue there is grouped by request date, because rows have no `paidAt`.
