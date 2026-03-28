#!/usr/bin/env bash
# Playwright E2E — Postgres (Docker) + API. Espelho de scripts/e2e-web.ps1 / e2e-ci-env.ps1.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

DOWN=0
QUICK=0
SKIP_DOCKER=0
for a in "$@"; do
  case "$a" in
    --down) DOWN=1 ;;
    --quick) QUICK=1 ;;
    --skip-docker) SKIP_DOCKER=1 ;;
  esac
done

if [[ "$DOWN" == "1" ]]; then
  echo "docker compose down..."
  docker compose down
  exit 0
fi

export ASPNETCORE_URLS='http://127.0.0.1:8080'
export DATABASE_URL_IDENTITY='Host=127.0.0.1;Port=5432;Database=parking_identity;Username=parking;Password=parking_dev'
export DATABASE_URL_AUDIT='Host=127.0.0.1;Port=5432;Database=parking_audit;Username=parking;Password=parking_dev'
export TENANT_DATABASE_URL_TEMPLATE='Host=127.0.0.1;Port=5432;Database=parking_{uuid};Username=parking;Password=parking_dev'
export JWT_SECRET='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
export PIX_WEBHOOK_SECRET='bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
export PIX_MODE='Stub'
export E2E_SEED='1'
export CORS_ORIGINS='http://127.0.0.1:5173'

if [[ "$SKIP_DOCKER" != "1" ]]; then
  echo "docker compose up -d --wait..."
  docker compose up -d --wait
else
  echo "SkipDocker: assumindo Postgres em 127.0.0.1:5432."
fi

echo "dotnet build API..."
dotnet build "$ROOT/backend/src/Parking.Api/Parking.Api.csproj" -c Release

API_LOG="${TMPDIR:-/tmp}/parking-api-e2e.log"
API_ERR="${TMPDIR:-/tmp}/parking-api-e2e.err"
: >"$API_LOG"
: >"$API_ERR"

echo "API em background..."
dotnet run --no-launch-profile --project "$ROOT/backend/src/Parking.Api/Parking.Api.csproj" --no-build -c Release >>"$API_LOG" 2>>"$API_ERR" &
API_PID=$!

cleanup() {
  if kill -0 "$API_PID" 2>/dev/null; then
    kill "$API_PID" 2>/dev/null || true
    wait "$API_PID" 2>/dev/null || true
  fi
  echo "Processo da API encerrado."
}
trap cleanup EXIT

ok=0
for _ in $(seq 1 60); do
  if curl -sf "http://127.0.0.1:8080/health" >/dev/null; then ok=1; break; fi
  sleep 2
done
if [[ "$ok" != "1" ]]; then
  echo "--- API log ---"; cat "$API_LOG" || true; echo "--- API err ---"; cat "$API_ERR" || true
  exit 1
fi

cd "$ROOT/frontend-web"
export CI=true
export E2E_API_ORIGIN='http://127.0.0.1:8080'
export E2E_API_BASE='http://127.0.0.1:8080/api/v1'

if [[ "$QUICK" != "1" ]]; then
  npm ci
  npx playwright install chromium
else
  if [[ ! -d node_modules ]]; then
    echo "Quick: node_modules ausente — npm ci"
    npm ci
  fi
  echo "Quick: sem npm ci / playwright install"
fi

npm run test:e2e
