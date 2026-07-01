# Core-API の OpenAPI JSON を service/api/openapi/core-api-v1.openapi.json に export する。
# 用法: リポジトリルートから .\scripts\export-core-api-openapi.ps1

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "service\api\statevia-api.sln"

Push-Location $repoRoot
try {
    $env:STATEVIA_EXPORT_OPENAPI = "true"
    dotnet test $solution `
        --filter "FullyQualifiedName~OpenApiDocumentTests.ExportOpenApiToRepository_WhenExportFlagSet" `
        --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "OpenAPI export test failed with exit code $LASTEXITCODE"
    }
    Write-Host "Wrote service/api/openapi/core-api-v1.openapi.json"
}
finally {
    Remove-Item Env:STATEVIA_EXPORT_OPENAPI -ErrorAction SilentlyContinue
    Pop-Location
}
