#Requires -Version 5.1
<#
.SYNOPSIS
  action-host 向け SonarScanner（begin → build → coverage → end）を実行する。

.DESCRIPTION
  スクリプト配置（リポジトリの sonar/）から action-host とカバレッジ出力パスを解決する。
  カレントディレクトリに依存しない。
  依存アセンブリ（engine / api / cli / ui / infrastructure）が混ざらないよう解析・カバレッジ除外を設定する。

.NOTES
  環境変数 SONAR_TOKEN を事前に設定すること。
  sonar-project.properties は SonarScanner for .NET では使わない（begin の /d: で指定）。
  プロジェクトキー: StateviaServiceActionHost
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 依存アセンブリ・生成物・ホストエントリを除外し、Action Host 本体のカバレッジを測る。
# Statevia.Core.Actions.Abstractions を旧パス名のまま残すと除外漏れで overall coverage が下がる。
$sonarAnalysisExclusions = @(
    '**/service/api/**',
    '**/api/**',
    '**/ui/studio/**',
    '**/core/engine/**',
    '**/engine/**',
    '**/core/actions/**',
    '**/service/cli/**',
    '**/cli/**',
    '**/service/runtime/**',
    '**/Statevia.Core.Engine/**',
    '**/Statevia.Core.Actions.Abstractions/**',
    '**/Statevia.Service.Api/**',
    '**/infrastructure/**',
    '**/Migrations/**',
    '**/Program.cs',
    '**/obj/**',
    '**/*.g.cs',
    '**/docker-compose.yml'
) -join ','
$sonarCoverageExclusions = $sonarAnalysisExclusions

if (-not $env:SONAR_TOKEN) {
    Write-Error '環境変数 SONAR_TOKEN が設定されていません。'
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$actionHostDir = Join-Path $repoRoot 'service\action-host'
$coverageXml = Join-Path $PSScriptRoot 'service-action-host-coverage.xml'

if (-not (Test-Path -LiteralPath $actionHostDir -PathType Container)) {
    Write-Error "service/action-host ディレクトリが見つかりません: $actionHostDir"
    exit 1
}

Push-Location -LiteralPath $actionHostDir
try {
    dotnet sonarscanner begin /k:"StateviaServiceActionHost" /n:"StateviaServiceActionHost" `
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

    dotnet build 'statevia-action-host.sln'
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
