#!/usr/bin/env bash
# SPEC v8.7 — verificação local alinhada ao CI (Unix).
# Uso:
#   ./scripts/verify.sh
#   INCLUDE_E2E=1 ./scripts/verify.sh       # + Playwright (Docker + API)
#   INCLUDE_ANDROID=1 ./scripts/verify.sh # + ./gradlew test (JAVA_HOME JDK 17)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "verify.sh: root=$ROOT"
test -f SPEC.md || { echo "Falta SPEC.md"; exit 1; }

INCLUDE_E2E="${INCLUDE_E2E:-0}"
INCLUDE_ANDROID="${INCLUDE_ANDROID:-0}"

if [[ -f backend/Parking.sln ]]; then
  SLN="backend/Parking.sln"
else
  SLN="$(find backend -name '*.sln' 2>/dev/null | head -1 || true)"
fi

if [[ -n "$SLN" && -f "$SLN" ]]; then
  echo "dotnet restore/build/test + SPEC §23.3 coverage (CI parity)"
  dotnet restore "$SLN"
  dotnet build "$SLN" -c Release --no-restore
  COV_OUT="$ROOT/backend-coverage-out"
  rm -rf "$COV_OUT"
  dotnet test "$SLN" -c Release --no-build \
    --settings backend/tests/Parking.Tests/coverlet.spec.runsettings \
    --collect:"XPlat Code Coverage" \
    --results-directory "$COV_OUT" \
    --verbosity normal
  COV="$(find "$COV_OUT" -name coverage.cobertura.xml -print -quit)"
  test -n "$COV" || { echo "coverage.cobertura.xml missing"; exit 1; }
  if command -v python3 >/dev/null 2>&1; then
    python3 "$ROOT/backend/scripts/check_spec_coverage.py" "$COV"
  elif command -v python >/dev/null 2>&1; then
    python "$ROOT/backend/scripts/check_spec_coverage.py" "$COV"
  else
    echo "verify.sh: Python not found — install for §23.3 threshold check."
  fi
else
  echo "Sem backend/*.sln — ignorando dotnet test."
fi

if [[ -f frontend-web/package.json ]]; then
  cd frontend-web
  if [[ -f package-lock.json ]]; then npm ci; else npm install; fi
  npm run lint
  npm run build
  npm run test
  if [[ "$INCLUDE_E2E" == "1" ]]; then
    echo "verify.sh: Playwright — npx playwright install chromium"
    npx playwright install chromium
  fi
  cd "$ROOT"
fi

if [[ "$INCLUDE_E2E" == "1" ]]; then
  echo "verify.sh: INCLUDE_E2E — scripts/e2e-web.sh --quick"
  chmod +x "$ROOT/scripts/e2e-web.sh"
  "$ROOT/scripts/e2e-web.sh" --quick
fi

if [[ "$INCLUDE_ANDROID" == "1" ]]; then
  if [[ -z "${JAVA_HOME:-}" ]]; then
    echo "INCLUDE_ANDROID=1 requer JAVA_HOME (JDK 17)" >&2
    exit 1
  fi
  echo "verify.sh: gradlew test"
  (cd android && chmod +x gradlew && ./gradlew test --no-daemon)
fi

echo "verify.sh: OK"
