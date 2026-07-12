[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$serviceName = "ThreeDManager"
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this uninstaller from an elevated PowerShell window."
}

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if (-not $service) {
    Write-Output "Service $serviceName is not installed."
    exit 0
}

if ($service.Status -ne "Stopped") {
    Stop-Service -Name $serviceName
    $service.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(30))
}

& sc.exe delete $serviceName | Out-Null
Write-Output "Service $serviceName was removed. Application files were preserved."
