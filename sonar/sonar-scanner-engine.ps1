#Requires -Version 5.1
<#
.SYNOPSIS
  engine 向け SonarScanner（begin → build → coverage → end）を実行する。

.DESCRIPTION
  スクリプト配置（リポジトリの sonar/）から engine とカバレッジ出力パスを解決する。
  カレントディレクトリに依存しない。

.NOTES
  環境変数 SONAR_TOKEN を事前に設定すること。
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $env:SONAR_TOKEN) {
    Write-Error '環境変数 SONAR_TOKEN が設定されていません。'
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$engineDir = Join-Path $repoRoot 'core\engine'
$coverageXml = Join-Path $PSScriptRoot 'core-engine-coverage.xml'

# Engine sln 内の *.Tests と他コンポーネントを除外
$sonarAnalysisExclusions = @(
    '**/service/**',
    '**/infrastructure/**',
    '**/ui/**',
    '**/infrastructure/**',
    '**/samples/**',
    '**/docker-compose.yml',
    '**/*.Tests/**'
) -join ','
$sonarCoverageExclusions = $sonarAnalysisExclusions

if (-not (Test-Path -LiteralPath $engineDir -PathType Container)) {
    Write-Error "core/engine ディレクトリが見つかりません: $engineDir"
    exit 1
}

Push-Location -LiteralPath $engineDir
try {
    dotnet sonarscanner begin /k:"StateviaCoreEngine" `
        /d:sonar.host.url="http://localhost:9000" `
        /d:sonar.token="$($env:SONAR_TOKEN)" `
        /d:sonar.projectBaseDir="$repoRoot" `
        /d:sonar.dotnet.excludeTestProjects=true `
        /d:sonar.cs.vscoveragexml.reportsPaths="$coverageXml" `
        "/d:sonar.exclusions=$sonarAnalysisExclusions" `
        "/d:sonar.coverage.exclusions=$sonarCoverageExclusions"
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] sonarscanner begin failed'
        exit 1
    }

    dotnet build 'statevia-engine.sln'
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
