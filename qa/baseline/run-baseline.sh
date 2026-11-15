#!/usr/bin/env bash
# Reset an isolated QA database, start ServiceApp.Web on a private port with FAKE
# SendGrid/Razorpay credentials, and run the black-box probe against it.
#
#   bash qa/run-baseline.sh                 # uses defaults below
#   QA_SERVER='(localdb)\AsmsQa' QA_PORT=5090 WEB_DIR=ServiceApp.Web bash qa/run-baseline.sh
#
# Requirements: dotnet 8 SDK + dotnet-ef, sqlcmd, python 3. Never points at the shared DB.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
QA_SERVER="${QA_SERVER:-(localdb)\\AsmsQa}"     # create with: sqllocaldb create AsmsQa -s
QA_DB="${QA_DB:-ServiceAppDB_QA}"
QA_PORT="${QA_PORT:-5090}"
WEB_DIR="${WEB_DIR:-ServiceApp.Web}"            # startup project (adjust after the restructure)
DATA_DIR="${DATA_DIR:-ServiceApp.Data}"         # migrations project
FAKE_RZP_SECRET="qa-fake-secret"

CS="Server=${QA_SERVER};Database=${QA_DB};Trusted_Connection=True;TrustServerCertificate=True;"

# Destructive reset is opt-in (QA_RESET=1). By default use a fresh DB name, e.g. QA_DB=ServiceAppDB_QA_$(date +%s)
if [ "${QA_RESET:-0}" = "1" ]; then
  echo "== reset ${QA_DB} on ${QA_SERVER}"
  sqlcmd -S "$QA_SERVER" -E -I -Q "IF DB_ID('${QA_DB}') IS NOT NULL BEGIN ALTER DATABASE [${QA_DB}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${QA_DB}]; END"
fi

echo "== build + migrate"
dotnet build "$ROOT/Automated-Service-Management-System.slnx" -v q -nologo >/dev/null
dotnet ef database update -p "$ROOT/$DATA_DIR" -s "$ROOT/$WEB_DIR" --no-build --connection "$CS" >/dev/null

echo "== start app on :${QA_PORT}"
export ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://localhost:${QA_PORT}"
export ConnectionStrings__DefaultConnection="$CS"
export SendGrid__ApiKey='SG.qa-invalid-key' SendGrid__FromEmail='qa@example.invalid'
export Razorpay__KeyId='rzp_test_qafake' Razorpay__KeySecret="$FAKE_RZP_SECRET" Razorpay__WebhookSecret='qa-fake-webhook'
LOG="$ROOT/qa/app-under-test.log"
( cd "$ROOT/$WEB_DIR" && exec dotnet run --no-launch-profile --no-build ) >"$LOG" 2>&1 &
APP_PID=$!
# stop ONLY the process listening on our port (never kill other ServiceApp instances)
stop_app() { powershell -NoProfile -c "Get-NetTCPConnection -LocalPort ${QA_PORT} -State Listen -EA SilentlyContinue | % { Stop-Process -Id \$_.OwningProcess -Force }" || true; kill $APP_PID 2>/dev/null || true; }
trap stop_app EXIT
for _ in $(seq 1 90); do grep -q "Now listening" "$LOG" && break; sleep 1; done

echo "== run probe"
ASMS_BASE_URL="http://localhost:${QA_PORT}" ASMS_RZP_SECRET="$FAKE_RZP_SECRET" ASMS_SQLCMD="$QA_SERVER" ASMS_SQLCMD_DB="$QA_DB" \
  PYTHONIOENCODING=utf-8 python -I "$ROOT/qa/asms_baseline_e2e.py" || true
echo "== server errors logged during run:"
grep -cE " ERR\]|responded 500" "$LOG" || true
