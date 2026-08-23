#Requires -Version 5.1
<#
.SYNOPSIS
  リファレンス Module（Http / Notification）向け SonarScanner（begin → build → coverage → end）を実行する。

.DESCRIPTION
  スクリプト配置（リポジトリの sonar/）から modules/reference とカバレッジ出力パスを解決する。
  カレントディレクトリに依存しない。
  公式提供 Module を StateviaModulesReference として解析し、API / Engine と二重計上しない。

.NOTES
  環境変数 SONAR_TOKEN を事前に設定すること。
  sonar-project.properties は SonarScanner for .NET では使わない（begin の /d: で指定）。
  プロジェクトキー: StateviaModulesReference
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 解析は modules/reference のプロダクションコードに限定する。
$sonarAnalysisExclusions = @(
    '**/service/**',
    '**/core/**',
    '**/infrastructure/**',
    '**/ui/**',
    '**/tests/**',
    '**/modules/default/**',
    '**/*.Tests/**',
    '**/Dockerfile'
) -join ','
$sonarCoverageExclusions = $sonarAnalysisExclusions

if (-not $env:SONAR_TOKEN) {
    Write-Error '環境変数 SONAR_TOKEN が設定されていません。'
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$referenceDir = Join-Path $repoRoot 'modules\reference'
$coverageXml = Join-Path $PSScriptRoot 'modules-reference-coverage.xml'

if (-not (Test-Path -LiteralPath $referenceDir -PathType Container)) {
    Write-Error "modules/reference ディレクトリが見つかりません: $referenceDir"
    exit 1
}

Push-Location -LiteralPath $referenceDir
try {
    dotnet sonarscanner begin /k:"StateviaModulesReference" /n:"StateviaModulesReference" `
        /d:sonar.host.url="http://localhost:9000" `
        /d:sonar.token="$($env:SONAR_TOKEN)" `
        /d:sonar.projectBaseDir="$repoRoot" `
        /d:sonar.dotnet.excludeTestProjects=true `
        /d:sonar.cs.vscoveragexml.reportsPaths="$coverageXml" `
        "/d:sonar.inclusions=**/modules/reference/**" `
        "/d:sonar.exclusions=$sonarAnalysisExclusions" `
        "/d:sonar.coverage.exclusions=$sonarCoverageExclusions"
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] sonarscanner begin failed'
        exit 1
    }

    dotnet build 'statevia-reference.sln'
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
