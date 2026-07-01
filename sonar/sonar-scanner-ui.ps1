#Requires -Version 5.1
<#
.SYNOPSIS
  UI（ui/studio）向け SonarScanner を実行する。

.DESCRIPTION
  スクリプト配置（リポジトリの sonar/）から ui/studio を解決し、
  カバレッジ生成（vitest）のあと sonar-scanner を実行する。
  カレントディレクトリに依存しない。

.NOTES
  環境変数 SONAR_TOKEN を事前に設定すること。
  Node.js / npm が PATH にあり、ui/studio で依存関係がインストール済みであること。
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $env:SONAR_TOKEN) {
    Write-Error '環境変数 SONAR_TOKEN が設定されていません。'
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$uiDir = Join-Path (Join-Path $repoRoot 'ui') 'studio'
$lcovPath = Join-Path $uiDir 'coverage\lcov.info'

if (-not (Test-Path -LiteralPath $uiDir -PathType Container)) {
    Write-Error "ui/studio ディレクトリが見つかりません: $uiDir"
    exit 1
}

Push-Location -LiteralPath $uiDir
try {
    npm run test:coverage
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] npm run test:coverage failed'
        exit 1
    }

    if (-not (Test-Path -LiteralPath $lcovPath -PathType Leaf)) {
        Write-Error "カバレッジファイルが生成されませんでした: $lcovPath"
        exit 1
    }

    npx --yes sonar-scanner "-Dsonar.token=$($env:SONAR_TOKEN)"
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] sonar-scanner failed'
        exit 1
    }
}
finally {
    Pop-Location
}

Write-Host '[OK] SonarQube analysis completed.'
