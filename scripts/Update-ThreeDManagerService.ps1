[CmdletBinding()]
param(
    [string]$PublishPath = (Join-Path $PSScriptRoot "..\artifacts\publish\ThreeDManager"),
    [string]$InstallPath = "C:\ProgramData\ThreeDManager\app"
)

$ErrorActionPreference = "Stop"
$serviceName = "ThreeDManager"
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this updater from an elevated PowerShell window."
}

$service = Get-Service -Name $serviceName -ErrorAction Stop
$resolvedPublishPath = (Resolve-Path -LiteralPath $PublishPath).Path
$sourceExecutable = Join-Path $resolvedPublishPath "ThreeDManager.Web.exe"
if (-not (Test-Path -LiteralPath $sourceExecutable)) {
    throw "Publish output is incomplete. Run scripts\Publish-ThreeDManager.ps1 first."
}
if (-not (Test-Path -LiteralPath $InstallPath)) {
    throw "Install path was not found: $InstallPath"
}

if ($service.Status -ne "Stopped") {
    Stop-Service -Name $serviceName
    $service.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(30))
}

try {
    Copy-Item -Path (Join-Path $resolvedPublishPath "*") -Destination $InstallPath -Recurse -Force
}
finally {
    Start-Service -Name $serviceName
    (Get-Service -Name $serviceName).WaitForStatus("Running", [TimeSpan]::FromSeconds(30))
}

$css = Invoke-WebRequest "http://127.0.0.1:5080/css/site.css" -UseBasicParsing
if ($css.StatusCode -ne 200 -or $css.Headers["Content-Type"] -notlike "text/css*") {
    throw "ThreeDManager restarted, but /css/site.css did not return CSS."
}

Write-Output "ThreeDManager was updated and is running with CSS enabled."
