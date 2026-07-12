[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\artifacts\publish\ThreeDManager")
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repositoryRoot "src\ThreeDManager.Web\ThreeDManager.Web.csproj"
$artifactsRoot = Join-Path $repositoryRoot "artifacts"
$resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)

if (-not $resolvedOutputPath.StartsWith($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The publish output must stay inside the repository artifacts directory."
}

dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output $resolvedOutputPath

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$executablePath = Join-Path $resolvedOutputPath "ThreeDManager.Web.exe"
if (-not (Test-Path -LiteralPath $executablePath)) {
    throw "Published executable was not found at $executablePath."
}

Write-Output $executablePath
