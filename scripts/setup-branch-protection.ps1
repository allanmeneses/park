# SPEC §25.5 — aplica branch protection em `main` via GitHub CLI (`gh`).
# Pré-requisitos: `gh auth login` com scope `repo`; Git remote `origin` → github.com/{owner}/{repo}
#
# Repositório solo: use -Approvals 0 (sem aprovação obrigatória de terceiros).
# Se o GitHub devolver 422 nos checks, confira os nomes exatos em Actions no último run verde
# e passe -Contexts 'Nome exato', ...

param(
    [string]$Branch = "main",
    [int]$Approvals = 1,
    [switch]$UseWorkflowPrefix,
    [string[]]$Contexts = @(
        "Spec documents",
        "Backend (.NET)",
        "Frontend Web (Vue)",
        "Frontend E2E (Playwright)",
        "Android unit (Gradle)",
        "Android instrumented (SPEC_FRONTEND §13.3–13.4)"
    )
)

$ErrorActionPreference = "Stop"
$remote = git remote get-url origin 2>$null
if (-not $remote) { throw "Sem remote git (defina origin)." }

if ($remote -match "github\.com[:/]([^/]+)/([^/.]+)(?:\.git)?") {
    $owner = $Matches[1]
    $repo = $Matches[2]
} else {
    throw "Remote não reconhecido como github.com: $remote"
}

$repoFull = "$owner/$repo"
$ctx = if ($UseWorkflowPrefix) { $Contexts | ForEach-Object { "ci / $_" } } else { $Contexts }
Write-Host "Repositório: $repoFull  Branch: $Branch  Aprovações PR: $Approvals  Prefixo 'ci /': $UseWorkflowPrefix"

$body = [ordered]@{
    required_status_checks  = @{
        strict   = $true
        contexts = @($ctx)
    }
    enforce_admins          = $true
    restrictions            = $null
    required_linear_history = $false
    allow_force_pushes      = $false
    allow_deletions         = $false
}
if ($Approvals -ge 1) {
    $body["required_pull_request_reviews"] = @{
        required_approving_review_count = $Approvals
        dismiss_stale_reviews           = $true
    }
}

$json = $body | ConvertTo-Json -Depth 6
$tmp = [System.IO.Path]::GetTempFileName()
try {
    [System.IO.File]::WriteAllText($tmp, $json, [System.Text.UTF8Encoding]::new($false))
    gh api -X PUT "repos/$repoFull/branches/$Branch/protection" --input $tmp
    Write-Host "Branch protection aplicada em $Branch."
} finally {
    Remove-Item -Force $tmp -ErrorAction SilentlyContinue
}
