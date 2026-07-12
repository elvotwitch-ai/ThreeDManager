[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$OperatorUsername,

    [string]$PublishPath = (Join-Path $PSScriptRoot "..\artifacts\publish\ThreeDManager"),

    [string]$InstallPath = "C:\ProgramData\ThreeDManager\app",

    [string]$ListenUrl = "http://127.0.0.1:5080"
)

$ErrorActionPreference = "Stop"
$serviceName = "ThreeDManager"
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this installer from an elevated PowerShell window."
}

if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    throw "Service $serviceName already exists. Uninstall it before installing a replacement."
}

$resolvedPublishPath = (Resolve-Path -LiteralPath $PublishPath).Path
$sourceExecutable = Join-Path $resolvedPublishPath "ThreeDManager.Web.exe"
if (-not (Test-Path -LiteralPath $sourceExecutable)) {
    throw "Publish output is incomplete. Run scripts\Publish-ThreeDManager.ps1 first."
}

if (Test-Path -LiteralPath $InstallPath) {
    $existingFiles = Get-ChildItem -LiteralPath $InstallPath -Force
    if ($existingFiles.Count -gt 0) {
        throw "Install path is not empty: $InstallPath"
    }
}
else {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

$securePassword = Read-Host "Password for alpha operator '$OperatorUsername'" -AsSecureString
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    $operatorPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    if ([string]::IsNullOrWhiteSpace($operatorPassword) -or $operatorPassword.Length -lt 12) {
        throw "The operator password must contain at least 12 characters."
    }

    Copy-Item -Path (Join-Path $resolvedPublishPath "*") -Destination $InstallPath -Recurse -Force
    $installedExecutable = Join-Path $InstallPath "ThreeDManager.Web.exe"

    New-Service `
        -Name $serviceName `
        -BinaryPathName ('"{0}"' -f $installedExecutable) `
        -DisplayName "ThreeDManager" `
        -Description "ThreeDManager ASP.NET Core alpha server" `
        -StartupType Automatic | Out-Null

    $serviceRegistryPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName"
    $serviceEnvironment = @(
        "ASPNETCORE_ENVIRONMENT=Production",
        "ASPNETCORE_URLS=$ListenUrl",
        "AlphaAccess__Username=$OperatorUsername",
        "AlphaAccess__Password=$operatorPassword"
    )
    New-ItemProperty `
        -Path $serviceRegistryPath `
        -Name Environment `
        -PropertyType MultiString `
        -Value $serviceEnvironment `
        -Force | Out-Null

    & sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/15000/restart/30000 | Out-Null
    Start-Service -Name $serviceName
    (Get-Service -Name $serviceName).WaitForStatus("Running", [TimeSpan]::FromSeconds(30))
}
finally {
    if ($passwordPointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    }
    $operatorPassword = $null
}

Write-Output "ThreeDManager is running at $ListenUrl."
