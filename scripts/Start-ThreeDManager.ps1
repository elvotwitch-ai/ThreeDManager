[CmdletBinding()]
param(
    # Rebuild the app and update the Windows service before opening the tunnel.
    # Requires elevation; the script self-elevates when this switch is used.
    [switch]$Build,

    # Local origin the tunnel points at (must match the service's binding).
    [string]$AppUrl = "http://127.0.0.1:5080",

    [string]$ServiceName = "ThreeDManager",
    [string]$Cloudflared = "C:\Program Files (x86)\cloudflared\cloudflared.exe",

    # Skip opening the default browser (useful for headless checks).
    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"
$scriptRoot = $PSScriptRoot

function Test-Admin {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# --- 1. Optional rebuild + redeploy (elevates itself if needed) ------------------
if ($Build) {
    if (-not (Test-Admin)) {
        Write-Host "Rebuild solicitado: reabrindo em PowerShell elevado..." -ForegroundColor Yellow
        $passthru = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $PSCommandPath, "-Build")
        if ($NoBrowser) { $passthru += "-NoBrowser" }
        Start-Process powershell -Verb RunAs -ArgumentList $passthru
        return
    }
    Write-Host "==> Publicando (Release)..." -ForegroundColor Cyan
    & (Join-Path $scriptRoot "Publish-ThreeDManager.ps1") | Out-Host
    Write-Host "==> Atualizando o servico Windows..." -ForegroundColor Cyan
    & (Join-Path $scriptRoot "Update-ThreeDManagerService.ps1") | Out-Host
}

# --- 2. Ensure the service is running -------------------------------------------
$svc = Get-Service -Name $ServiceName -ErrorAction Stop
if ($svc.Status -ne "Running") {
    Write-Host "==> Iniciando o servico '$ServiceName'..." -ForegroundColor Cyan
    Start-Service -Name $ServiceName
    $svc.WaitForStatus("Running", [TimeSpan]::FromSeconds(30))
}

# --- 3. Wait for the local app to answer ----------------------------------------
Write-Host "==> Aguardando o app responder em $AppUrl ..." -ForegroundColor Cyan
$appReady = $false
for ($i = 0; $i -lt 30; $i++) {
    try {
        # Redirects are followed (302 -> /Account/Login -> 200), which proves the app is up.
        Invoke-WebRequest -Uri $AppUrl -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop | Out-Null
        $appReady = $true; break
    }
    catch {
        # A 4xx/5xx still means the app answered; only a connection error carries no response.
        if ($_.Exception.Response) { $appReady = $true; break }
        Start-Sleep -Seconds 1
    }
}
if (-not $appReady) { throw "O app nao respondeu em $AppUrl. Verifique o servico '$ServiceName'." }

# --- 4. Replace any stray quick tunnel so we keep exactly one --------------------
Get-Process -Name "cloudflared" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "==> Encerrando cloudflared anterior (PID $($_.Id))..." -ForegroundColor DarkYellow
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path -LiteralPath $Cloudflared)) {
    throw "cloudflared nao encontrado em '$Cloudflared'. Ajuste o parametro -Cloudflared."
}

# --- 5. Start the quick tunnel and capture the generated URL ---------------------
$logOut = Join-Path $env:TEMP "threedmanager-tunnel.out.log"
$logErr = Join-Path $env:TEMP "threedmanager-tunnel.err.log"
Remove-Item $logOut, $logErr -Force -ErrorAction SilentlyContinue

Write-Host "==> Abrindo o tunel Cloudflare..." -ForegroundColor Cyan
$proc = Start-Process -FilePath $Cloudflared `
    -ArgumentList @("tunnel", "--no-autoupdate", "--url", $AppUrl) `
    -RedirectStandardOutput $logOut `
    -RedirectStandardError $logErr `
    -WindowStyle Hidden -PassThru

$publicUrl = $null
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Seconds 1
    if ($proc.HasExited) { throw "cloudflared encerrou cedo. Log: $logErr" }
    $text = (Get-Content @($logErr, $logOut) -Raw -ErrorAction SilentlyContinue) -join "`n"
    $match = [regex]::Match($text, "https://[a-z0-9-]+\.trycloudflare\.com")
    if ($match.Success) { $publicUrl = $match.Value; break }
}
if (-not $publicUrl) {
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
    throw "Tempo esgotado aguardando a URL do tunel. Log: $logErr"
}

# --- 6. Wait until the public URL is actually reachable --------------------------
Write-Host "==> Aguardando o tunel ficar acessivel..." -ForegroundColor Cyan
for ($i = 0; $i -lt 30; $i++) {
    try {
        Invoke-WebRequest -Uri $publicUrl -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop | Out-Null
        break
    }
    catch {
        if ($_.Exception.Response) { break }   # any HTTP response means it's live
        Start-Sleep -Seconds 2
    }
}

try { Set-Clipboard -Value $publicUrl } catch { }

Write-Host ""
Write-Host "  ThreeDManager acessivel em:" -ForegroundColor Green
Write-Host "  $publicUrl" -ForegroundColor Green
Write-Host "  (URL copiada para a area de transferencia)" -ForegroundColor DarkGray
Write-Host ""

if (-not $NoBrowser) { Start-Process $publicUrl }

# --- 7. Keep the tunnel alive while this window stays open -----------------------
try {
    Write-Host "Tunel ativo. Feche esta janela ou pressione Ctrl+C para encerrar o acesso externo." -ForegroundColor Yellow
    Wait-Process -Id $proc.Id
}
finally {
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
    Write-Host "Tunel encerrado." -ForegroundColor DarkYellow
}
