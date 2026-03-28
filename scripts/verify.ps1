# SPEC v8.7 — verificação local alinhada ao CI (Windows).
# Backend: dotnet test com cobertura §23.3 + check_spec_coverage.py (se Python no PATH).
# Uso:
#   .\scripts\verify.ps1                    # backend + frontend (build + Vitest)
#   .\scripts\verify.ps1 -IncludeE2E        # + Playwright (Docker + API; instala Chromium se preciso)
#   .\scripts\verify.ps1 -IncludeAndroid    # + ./gradlew test (JDK 17 + Android SDK; ver mensagem se faltar)
# Para “tudo que o repo testa” numa máquina completa: -IncludeE2E -IncludeAndroid
param(
    [switch] $IncludeE2E,
    [switch] $IncludeAndroid
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "verify.ps1: raiz = $root"

if (-not (Test-Path 'SPEC.md')) { throw 'Falta SPEC.md na raiz.' }

$sln = $null
if (Test-Path 'backend\Parking.sln') { $sln = 'backend\Parking.sln' }
else {
    $found = Get-ChildItem -Path backend -Filter '*.sln' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { $sln = $found.FullName }
}

if ($sln) {
    Write-Host "dotnet restore/build/test + SPEC §23.3 cobertura (paridade com CI)"
    dotnet restore $sln
    dotnet build $sln -c Release --no-restore
    $covOut = Join-Path $root 'backend-coverage-out'
    if (Test-Path $covOut) { Remove-Item -Recurse -Force $covOut }
    dotnet test $sln -c Release --no-build `
        --settings backend/tests/Parking.Tests/coverlet.spec.runsettings `
        --collect:"XPlat Code Coverage" `
        --results-directory $covOut `
        --verbosity normal
    $cov = Get-ChildItem -Path $covOut -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $cov) { throw 'coverage.cobertura.xml nao gerado (dotnet test / coverlet).' }
    $py = Get-Command python -ErrorAction SilentlyContinue
    if (-not $py) { $py = Get-Command python3 -ErrorAction SilentlyContinue }
    if ($py) {
        & $py.Source (Join-Path $root 'backend/scripts/check_spec_coverage.py') $cov.FullName
    } else {
        Write-Host 'verify.ps1: Python nao encontrado — instale Python para validar limiares §23.3 (check_spec_coverage.py).'
    }
} else {
    Write-Host 'Sem backend/Parking.sln - ignorando dotnet test.'
}

if (Test-Path 'frontend-web\package.json') {
    Set-Location frontend-web
    if (Test-Path 'package-lock.json') { npm ci } else { npm install }
    npm run lint
    npm run build
    npm run test
    if ($IncludeE2E) {
        Write-Host 'verify.ps1: Playwright - npx playwright install chromium'
        npx playwright install chromium
    }
    Set-Location $root
}

if ($IncludeE2E) {
    Write-Host 'verify.ps1: IncludeE2E - scripts\e2e-web.ps1 -Quick'
    & "$PSScriptRoot\e2e-web.ps1" -Quick
}

if ($IncludeAndroid) {
    if (-not $env:JAVA_HOME) {
        $jdk17 = "${env:ProgramFiles}\Java\jdk-17"
        $studioJbr = "${env:ProgramFiles}\Android\Android Studio\jbr"
        if (Test-Path "$jdk17\bin\java.exe") {
            $env:JAVA_HOME = $jdk17
            Write-Host "verify.ps1: JAVA_HOME = $jdk17 (auto)"
        }
        elseif (Test-Path "$studioJbr\bin\java.exe") {
            $env:JAVA_HOME = $studioJbr
            Write-Host "verify.ps1: JAVA_HOME = $studioJbr (auto)"
        }
        else {
            throw @'
IncludeAndroid: defina JAVA_HOME (JDK 17), por exemplo:
  [Environment]::SetEnvironmentVariable('JAVA_HOME','C:\Program Files\Java\jdk-17','User')
  $env:JAVA_HOME = 'C:\Program Files\Java\jdk-17'
'@
        }
    }
    if (-not (Test-Path 'android\gradlew.bat')) {
        throw 'IncludeAndroid: falta android\gradlew.bat'
    }

    $sdk = $env:ANDROID_HOME
    if ([string]::IsNullOrWhiteSpace($sdk)) { $sdk = $env:ANDROID_SDK_ROOT }
    if ([string]::IsNullOrWhiteSpace($sdk)) {
        $cand = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
        if (Test-Path $cand) { $sdk = $cand }
    }
    if ([string]::IsNullOrWhiteSpace($sdk) -or -not (Test-Path $sdk)) {
        throw @"
IncludeAndroid: Android SDK nao encontrado.
  Instale o Android Studio (aba SDK Manager) ou defina ANDROID_HOME.
  Depois crie android\local.properties com uma linha (use barras /):
    sdk.dir=C:/Users/SEU_USUARIO/AppData/Local/Android/Sdk
  Modelo: android\local.properties.example
"@
    }
    $env:ANDROID_HOME = $sdk
    $env:ANDROID_SDK_ROOT = $sdk
    $lp = Join-Path $root 'android\local.properties'
    $sdkProp = $sdk.Replace('\', '/')
    $needWrite = $true
    if (Test-Path $lp) {
        $existing = Get-Content $lp -Raw -ErrorAction SilentlyContinue
        if ($existing -match [regex]::Escape($sdkProp)) { $needWrite = $false }
    }
    if ($needWrite) {
        "sdk.dir=$sdkProp" | Set-Content -Path $lp -Encoding utf8
        Write-Host "verify.ps1: escrito android\local.properties -> sdk.dir=$sdkProp"
    }

    Write-Host 'verify.ps1: IncludeAndroid - gradlew test'
    Push-Location android
    try {
        .\gradlew.bat test --no-daemon
    }
    finally {
        Pop-Location
    }
}

Write-Host "verify.ps1: OK"
