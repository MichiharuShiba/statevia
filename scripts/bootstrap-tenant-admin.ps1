# 初回テナント管理者を DB に作成する（Principal + User + user_principals）。
# 用法: リポジトリルートから
#   $env:STATEVIA_BOOTSTRAP_PASSWORD = "dev-only-password"
#   .\scripts\bootstrap-tenant-admin.ps1 -Email "admin@example.com"
# または
#   .\scripts\bootstrap-tenant-admin.ps1 -Email "admin@example.com" -Password "dev-only-password"

param(
    [string]$TenantKey = "default",
    [Parameter(Mandatory = $true)]
    [string]$Email,
    [string]$Password = $env:STATEVIA_BOOTSTRAP_PASSWORD,
    [string]$DisplayName = "",
    [string]$DatabaseUrl = "",
    [string]$Config = "",
    [switch]$SkipIfExists
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "api\Statevia.Core.Api.Bootstrap\Statevia.Core.Api.Bootstrap.csproj"

if ([string]::IsNullOrWhiteSpace($Password)) {
    throw "Set -Password or environment variable STATEVIA_BOOTSTRAP_PASSWORD."
}

$dotnetArgs = @(
    "run",
    "--project", $project,
    "--"
)

if (-not [string]::IsNullOrWhiteSpace($DatabaseUrl)) {
    $dotnetArgs += @("--database-url", $DatabaseUrl)
}

if (-not [string]::IsNullOrWhiteSpace($Config)) {
    $dotnetArgs += @("--config", $Config)
}

$dotnetArgs += @(
    "create-admin",
    "--tenant-key", $TenantKey,
    "--email", $Email,
    "--password", $Password
)

if (-not [string]::IsNullOrWhiteSpace($DisplayName)) {
    $dotnetArgs += @("--display-name", $DisplayName)
}

if ($SkipIfExists) {
    $dotnetArgs += "--skip-if-exists"
}

Push-Location $repoRoot
try {
    & dotnet @dotnetArgs
    if ($LASTEXITCODE -ne 0) {
        throw "bootstrap-tenant-admin failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
