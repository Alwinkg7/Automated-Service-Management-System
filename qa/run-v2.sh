#!/usr/bin/env bash
# Regression run for the split architecture (ServiceApp.Api + ServiceApp.Web).
#
#   bash qa/run-v2.sh
#   SRC_DIR=/a/ASMS/asms-qa-v2 QA_DB=ServiceAppDB_QA_v2 bash qa/run-v2.sh
#
# * Publishes both hosts into a PRIVATE output dir (OUT_DIR) so no other agent's binaries are locked.
# * Uses a fresh DB on (localdb)\AsmsQa (the API migrates it on startup in Development).
# * Fake Razorpay/SendGrid credentials + a QA JWT key + Seed__AdminPassword via env vars only
#   (user-secrets are still loaded but every sensitive value is overridden).
# * Stops only the processes it started (by the ports it owns).
set -euo pipefail
QA_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC_DIR="${SRC_DIR:-$(cd "$QA_DIR/.." && pwd)}"
OUT_DIR="${OUT_DIR:-${TMPDIR:-/tmp}/asms-qa-out}"
QA_SERVER="${QA_SERVER:-(localdb)\\AsmsQa}"
QA_DB="${QA_DB:-ServiceAppDB_QA_v2}"
API_PORT="${API_PORT:-5090}"
WEB_PORT="${WEB_PORT:-5091}"
ADMIN_PW="${ADMIN_PW:-QaAdmin#2026x}"
FAKE_RZP_SECRET="qa-fake-secret"
FAKE_RZP_WH="qa-fake-webhook"

CS="Server=${QA_SERVER};Database=${QA_DB};Trusted_Connection=True;TrustServerCertificate=True;"

echo "== publish hosts from $SRC_DIR -> $OUT_DIR"
dotnet publish "$SRC_DIR/ServiceApp.Api/ServiceApp.Api.csproj" -c Debug -o "$OUT_DIR/api" -nologo -v q >/dev/null
dotnet publish "$SRC_DIR/ServiceApp.Web/ServiceApp.Web.csproj" -c Debug -o "$OUT_DIR/web" -nologo -v q >/dev/null

export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__DefaultConnection="$CS"
export Jwt__Key="qa-only-jwt-signing-key-0123456789abcdef-$(date +%s)"
export Seed__AdminPassword="$ADMIN_PW"
export SendGrid__ApiKey='SG.qa-invalid-key' SendGrid__FromEmail='qa@example.invalid' Notifications__AdminEmail='qa-admin@example.invalid'
export Razorpay__KeyId='rzp_test_qafake' Razorpay__KeySecret="$FAKE_RZP_SECRET" Razorpay__WebhookSecret="$FAKE_RZP_WH"
export SignalR__RedisConnectionString=''

API_LOG="$QA_DIR/logs/api.log"; WEB_LOG="$QA_DIR/logs/web.log"; mkdir -p "$QA_DIR/logs"
stop_ports() {
  for p in "$API_PORT" "$WEB_PORT"; do
    powershell -NoProfile -c "Get-NetTCPConnection -LocalPort $p -State Listen -EA SilentlyContinue | % { Stop-Process -Id \$_.OwningProcess -Force }" >/dev/null 2>&1 || true
  done
}
trap stop_ports EXIT

echo "== start API :$API_PORT (migrates $QA_DB)"
( cd "$OUT_DIR/api" && ASPNETCORE_URLS="http://localhost:$API_PORT" exec dotnet ServiceApp.Api.dll ) >"$API_LOG" 2>&1 &
for _ in $(seq 1 120); do curl -sf "http://localhost:$API_PORT/health" >/dev/null 2>&1 && break; sleep 1; done
echo "== start Web :$WEB_PORT"
( cd "$OUT_DIR/web" && ASPNETCORE_URLS="http://localhost:$WEB_PORT" exec dotnet ServiceApp.Web.dll ) >"$WEB_LOG" 2>&1 &
for _ in $(seq 1 120); do curl -sf "http://localhost:$WEB_PORT/health" >/dev/null 2>&1 && break; sleep 1; done

echo "== probe"
ASMS_API_URL="http://localhost:$API_PORT" ASMS_WEB_URL="http://localhost:$WEB_PORT" ASMS_ADMIN_PASSWORD="$ADMIN_PW" \
ASMS_RZP_SECRET="$FAKE_RZP_SECRET" ASMS_RZP_WEBHOOK_SECRET="$FAKE_RZP_WH" ASMS_SQLCMD="$QA_SERVER" ASMS_SQLCMD_DB="$QA_DB" \
ASMS_API_LOG="$API_LOG" ASMS_WEB_LOG="$WEB_LOG" ASMS_SIGNALR_MODULE="${ASMS_SIGNALR_MODULE:-$SRC_DIR/frontend/node_modules/@microsoft/signalr}" \
PYTHONIOENCODING=utf-8 python -I "$QA_DIR/asms_e2e_v2.py" || true
echo "== error lines: API $(grep -cE ' ERR\]|responded 500' "$API_LOG" || true), WEB $(grep -cE ' ERR\]|responded 500' "$WEB_LOG" || true)"
