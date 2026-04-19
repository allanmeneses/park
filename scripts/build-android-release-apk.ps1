# Gera APK release (API de produção em BuildConfig) instalável no telemóvel.
# A variante release usa parking.api.production — ver android/local.properties.example.
#
# Uso:
#   .\scripts\build-android-release-apk.ps1
#   .\scripts\build-android-release-apk.ps1 -ProductionApiUrl "https://sua-api.../api/v1"
#   $env:PARKING_API_PRODUCTION="https://..." ; .\scripts\build-android-release-apk.ps1
#
# Precedência da URL no Gradle: -Pparking.api.production (quando o script passa) > local.properties
# > env PARKING_API_PRODUCTION > default de exemplo.
#
# Saída: copia para dist/parking-release-YYYYMMDD-HHmmss.apk (pasta dist/ está no .gitignore).
param(
    [string] $ProductionApiUrl = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$url = $ProductionApiUrl.Trim()
if (-not $url -and $env:PARKING_API_PRODUCTION) {
    $url = $env:PARKING_API_PRODUCTION.Trim()
}

$gradleArgs = @('assembleRelease', '--no-daemon')
if ($url) {
    $gradleArgs = @("-Pparking.api.production=$url") + $gradleArgs
}

Push-Location (Join-Path $root 'android')
try {
    & .\gradlew.bat @gradleArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally {
    Pop-Location
}

$apk = Join-Path $root 'android\app\build\outputs\apk\release\app-release.apk'
if (-not (Test-Path $apk)) {
    Write-Error "APK não encontrado: $apk"
}

$outDir = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$dest = Join-Path $outDir "parking-release-$stamp.apk"
Copy-Item -Path $apk -Destination $dest -Force
Write-Host "APK copiado para: $dest"
