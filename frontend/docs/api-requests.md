# Frontend ↔ API alignment

Every endpoint the frontend asked for is implemented and documented in [`docs/API.md`](../../docs/API.md). The frontend is aligned with those docs as of `d624f29`. `src/lib/api/schemas.ts` and `src/lib/api/endpoints/*` mirror them, and `npm run test:integration` checks them against the live API.

## Differences we adopted (from docs/API.md "Frontend alignment")

| Topic                                                         | API behaviour                                                                                     | Frontend                                                                                                             |
| ------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| Dispute reasons                                               | adds `JobIncomplete`                                                                              | in `DISPUTE_REASONS`                                                                                                 |
| Withdrawal create                                             | `data: { id, withdrawal }`                                                                        | schema accepts both                                                                                                  |
| Zone `pinCodes`                                               | `string[]` in responses, comma string in requests                                                 | normalised in `zoneSchema`; form sends a comma string                                                                |
| Admin `GET /requests`                                         | paged only when `page`/`pageSize` sent                                                            | always sends `page` + `pageSize=20`; filters/paging in SQL                                                           |
| Razorpay order                                                | `amount` in paise, already discounted; adds `subtotal`/`discount`/`payable`                       | discounts applied first via `quote` → `promo/preview` → `promo` / `points`; order then opened with the server amount |
| Unknown promo code                                            | 400 (already used: 409)                                                                           | shown inline on the pay page                                                                                         |
| Lists with `counts` (`/admin/disputes`, `/admin/technicians`) | counts are siblings of `data`                                                                     | client option `envelope: "full"`                                                                                     |
| Cash                                                          | customer `POST bills/{id}/cash` records the choice; technician `POST jobs/{id}/complete` confirms | "I'll pay in cash" on the pay page; "Confirm cash collected" on the job page                                         |
| Location                                                      | REST `jobs/{id}/location` exists                                                                  | frontend keeps using hub `ShareLocation`/`StopSharing`                                                               |

## Verified end to end (2026-11-15, `npm run test:integration`)

All of this ran through the Next.js BFF (cookies → `/api/proxy`) against `ServiceApp.Api` on `:5001` (LocalDB `AsmsQa`), with fake Razorpay credentials and an invalid SendGrid key:

- Register customer/technician (httpOnly cookies), admin login, a bad-password 401, and page guards (307 to login, 403 for the wrong role).
- Silent refresh when the access cookie is missing, then logout revoking the refresh token (replay gets 401).
- Booking with a repeated `Idempotency-Key` returns the same `requestId`.
- Admin paged search, server CSV, the technician list with counts, assignment, auto-assign, and technician deactivate/activate.
- Technician accept → bill → bill PDF.
- Customer quote, unknown promo → 400, points with no balance → 422, Razorpay order with fake creds → 502 (no charge), cash choice, customer PDF.
- Promo create → preview → apply (payable 700 → 650), apply again → 409, delete of a used code → 409.
- Cash confirmation → Completed, then history totals.
- Dispute 400 (short description) and 409 (duplicate); review, refund of a cash-paid job → 422, resolve.
- Wallet, a withdrawal above the available amount → 422, then create → approve → pay.
- Admin dashboard, analytics, users, zones (invalid zone → 400).
- SignalR negotiate with the BFF-issued token from origin `http://localhost:3000` → 200 with credentials CORS.

## Remaining backend notes (non-blocking)

1. **No realtime events for disputes, withdrawals or promo changes.** Those admin lists refresh on focus or after actions, but not live. Optional: hub events such as `DisputeRaised` and `WithdrawalRequested` to the `admin` group, and `WithdrawalUpdated` to the technician.
2. **Idempotency store is in-memory** (per instance, lost on restart). This is already noted in docs/API.md and needs Redis when scaled out.
3. **`GET /api/customer/requests` has no paging.** This is fine for per-customer volumes. Add `page`/`pageSize` like the admin list if needed.
4. **Display dates are pre-formatted strings** (`"20 Nov 2026"`) on request and job objects. The frontend parses them. ISO `*Utc` siblings on all rows (admin rows already have `createdAtUtc`) would let the UI localise consistently.
