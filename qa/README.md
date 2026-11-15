# QA assets

Black-box regression suite for ASMS. It covers the API host (`ServiceApp.Api`) and the MVC host (`ServiceApp.Web`) together.

| File | Purpose |
|---|---|
| `run-v2.sh` | Publishes both hosts into a private output dir and starts the API (which migrates a fresh QA DB) and then MVC. Runs the probe, then stops only the processes on its own ports. |
| `asms_e2e_v2.py` | The probe. Python stdlib only. Check ids keep the baseline bug tags `BUG-01..23`; new findings are tagged `NEW-xx`. Writes `last-run-v2.json`. |
| `signalr_isolation.mjs` | SignalR WebSocket group-isolation checks, using `frontend/node_modules/@microsoft/signalr`. Called by the probe. |
| `last-run-v2.json` | Results of the last run, kept as evidence. |
| `baseline/` | The pre-split audit (commit e06e39e): its probe, its runner, a `.http` file and its results. Use it for before/after comparisons. |

## Run

```bash
# prerequisites: dotnet 8 SDK, sqlcmd, python 3, node 20+, frontend deps installed (for the SignalR client)
sqllocaldb create AsmsQa -s          # once
QA_DB=ServiceAppDB_QA_$(date +%s) bash qa/run-v2.sh
```

Defaults:

- API at `http://localhost:5090`, MVC at `http://localhost:5091`
- Database server `(localdb)\AsmsQa`
- Change them with `API_PORT`, `WEB_PORT`, `QA_SERVER`, `QA_DB`, `SRC_DIR` and `OUT_DIR`.

Every secret is overridden through environment variables, so user-secrets values are never used:

- Razorpay and SendGrid get **fake** credentials, so no real charge or email can happen.
- The JWT key is a QA-only value.
- `Seed__AdminPassword` is set to a QA-only password.

The full run takes about 10 minutes. Most of that time is pacing, because the `auth` rate limit allows 10 requests per minute per IP.

## Test seams (QA database only)

1. **Razorpay order.** The fake keys make `razorpay/order` fail with a 502. The probe therefore writes a synthetic id into `Bills.RazorpayOrderId`, which stands in for a created order. It then forges the checkout and webhook HMACs with the fake secrets. This exercises order binding, amount checks and idempotency.
2. **Recurring auto-assign.** The probe first checks that the API registered the recurring job. It then removes the 2-minute `auto-assign` recurring job from the Hangfire tables, so that manual-assignment checks are deterministic. Auto-assignment is still tested through `POST /api/admin/auto-assign` and the MVC "Run auto-assign" button. The API re-registers the job on its next start.
