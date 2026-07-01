#Requires -Version 5.1
<#
.SYNOPSIS
  api 向け SonarScanner（begin → build → coverage → end）を実行する。

.DESCRIPTION
  スクリプト配置（リポジトリの sonar/）から api とカバレッジ出力パスを解決する。
  カレントディレクトリに依存しない。
  解析・カバレッジ除外は api/coverage.runsettings の意図（Engine / Program / Migrations）に合わせる。
  誤ってリポジトリルート等から実行した場合に UI / engine が混ざらないよう、ui/studio と core/engine も除外する。

.NOTES
  環境変数 SONAR_TOKEN を事前に設定すること。
  sonar-project.properties は SonarScanner for .NET では使わない（begin の /d: で指定）。
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# api/coverage.runsettings の Exclude / ExcludeByFile と整合
# sonar.projectBaseDir をリポジトリルートに固定し、移動後パス（core/engine 等）でも除外が効くようにする
$sonarAnalysisExclusions = @(
    '**/ui/studio/**',
    '**/services/ui/**',
    '**/core/engine/**',
    '**/engine/**',
    '**/service/cli/**',
    '**/service/action-host/**',
    '**/Statevia.Core.Engine/**',
    '**/Migrations/**',
    '**/Program.cs',
    '**/docker-compose.yml'
) -join ','
$sonarCoverageExclusions = $sonarAnalysisExclusions

if (-not $env:SONAR_TOKEN) {
    Write-Error '環境変数 SONAR_TOKEN が設定されていません。'
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiDir = Join-Path $repoRoot 'service\api'
$coverageXml = Join-Path $PSScriptRoot 'core-api-coverage.xml'

if (-not (Test-Path -LiteralPath $apiDir -PathType Container)) {
    Write-Error "service/api ディレクトリが見つかりません: $apiDir"
    exit 1
}

Push-Location -LiteralPath $apiDir
try {
    dotnet sonarscanner begin /k:"StateviaCoreAPI" /n:"StateviaCoreAPI" `
        /d:sonar.host.url="http://localhost:9000" `
        /d:sonar.token="$($env:SONAR_TOKEN)" `
        /d:sonar.projectBaseDir="$repoRoot" `
        /d:sonar.cs.vscoveragexml.reportsPaths="$coverageXml" `
        "/d:sonar.exclusions=$sonarAnalysisExclusions" `
        "/d:sonar.coverage.exclusions=$sonarCoverageExclusions"
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] sonarscanner begin failed'
        exit 1
    }

    dotnet build 'statevia-api.sln'
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] build failed'
        exit 1
    }

    dotnet-coverage collect 'dotnet test' -f xml -o "$coverageXml"
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] test / coverage failed'
        exit 1
    }

    dotnet sonarscanner end /d:sonar.token="$($env:SONAR_TOKEN)"
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] sonarscanner end failed'
        exit 1
    }
}
finally {
    Pop-Location
}

Write-Host '[OK] SonarQube analysis completed.'
