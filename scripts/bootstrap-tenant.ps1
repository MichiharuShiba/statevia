# 追加テナント（tenants 行）を作成する。
# 用法: リポジトリルートから
#   .\scripts\bootstrap-tenant.ps1 -TenantKey "acme-corp" -DisplayName "Acme Corporation"

param(
    [Parameter(Mandatory = $true)]
    [string]$TenantKey,
    [string]$DisplayName = "",
    [string]$DatabaseUrl = "",
    [string]$Config = "",
    [switch]$SkipIfExists
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "api\Statevia.Core.Api.Bootstrap\Statevia.Core.Api.Bootstrap.csproj"

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
    "create-tenant",
    "--tenant-key", $TenantKey
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
        throw "bootstrap-tenant failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
