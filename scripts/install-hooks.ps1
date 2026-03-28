# SPEC v8.7 §25.3 — ativa .githooks neste clone (Windows / PowerShell)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
git config core.hooksPath .githooks
Write-Host "core.hooksPath = .githooks (OK)"
