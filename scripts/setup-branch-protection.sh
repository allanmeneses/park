#!/usr/bin/env bash
# SPEC §25.5 — branch protection em `main` via `gh api`.
# Uso: ./scripts/setup-branch-protection.sh [branch] [approvals]
# Solo (sem revisão obrigatória): ./scripts/setup-branch-protection.sh main 0

set -euo pipefail
BRANCH="${1:-main}"
APPROVALS="${2:-1}"

REMOTE="$(git remote get-url origin)"
if [[ "$REMOTE" =~ github\.com[:/]([^/]+)/([^/.]+)(\.git)?$ ]]; then
  OWNER="${BASH_REMATCH[1]}"
  REPO="${BASH_REMATCH[2]}"
else
  echo "Remote não é github.com: $REMOTE" >&2
  exit 1
fi

REPO_FULL="$OWNER/$REPO"
CONTEXTS='["Spec documents","Backend (.NET)","Frontend Web (Vue)","Frontend E2E (Playwright)","Android unit (Gradle)","Android instrumented (SPEC_FRONTEND §13.3–13.4)"]'

if [[ "$APPROVALS" -lt 1 ]]; then
  BODY="$(jq -n --argjson ctx "$CONTEXTS" '{
    required_status_checks: { strict: true, contexts: $ctx },
    enforce_admins: true,
    restrictions: null,
    required_linear_history: false,
    allow_force_pushes: false,
    allow_deletions: false
  }')"
else
  BODY="$(jq -n --argjson ctx "$CONTEXTS" --argjson n "$APPROVALS" '{
    required_status_checks: { strict: true, contexts: $ctx },
    enforce_admins: true,
    required_pull_request_reviews: { required_approving_review_count: $n, dismiss_stale_reviews: true },
    restrictions: null,
    required_linear_history: false,
    allow_force_pushes: false,
    allow_deletions: false
  }')"
fi

echo "PUT repos/$REPO_FULL/branches/$BRANCH/protection (aprovações=$APPROVALS)"
printf '%s' "$BODY" | gh api -X PUT "repos/$REPO_FULL/branches/$BRANCH/protection" --input -
