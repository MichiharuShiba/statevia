<#
.SYNOPSIS
    Core-API テストを実行するラッパースクリプト。

.DESCRIPTION
    statevia-api.sln のテストを実行する。シナリオテスト（Docker が必要）の実行有無を
    スイッチや環境変数で制御できる。

.PARAMETER SkipScenario
    シナリオテスト（Category=Scenario）を除外して実行する。
    環境変数 STATEVIA_SKIP_SCENARIO_TESTS=true でも同等の動作になる。

.PARAMETER ScenarioOnly
    シナリオテスト（Category=Scenario）のみを実行する。

.EXAMPLE
    # デフォルト（全テスト。Docker なし環境ではシナリオは Skipped）
    ./scripts/test-api.ps1

.EXAMPLE
    # シナリオを除外して高速実行
    ./scripts/test-api.ps1 -SkipScenario

.EXAMPLE
    # シナリオのみ実行
    ./scripts/test-api.ps1 -ScenarioOnly

.EXAMPLE
    # 環境変数でシナリオを除外（CI 等での利用）
    $env:STATEVIA_SKIP_SCENARIO_TESTS = 'true'
    ./scripts/test-api.ps1
#>
[CmdletBinding()]
param(
    [switch] $SkipScenario,
    [switch] $ScenarioOnly
)

$ErrorActionPreference = 'Stop'

$slnPath = Join-Path $PSScriptRoot '..\service\api\statevia-api.sln'

# 環境変数によるシナリオ除外フラグの読み取り
$envSkip = $env:STATEVIA_SKIP_SCENARIO_TESTS -eq 'true'

$filterArg = @()

if ($ScenarioOnly) {
    $filterArg = '--filter', 'Category=Scenario'
    Write-Host 'シナリオテストのみを実行します（Category=Scenario）'
}
elseif ($SkipScenario -or $envSkip) {
    $filterArg = '--filter', 'Category!=Scenario'
    Write-Host 'シナリオテストを除外して実行します（Category!=Scenario）'
}
else {
    Write-Host '全テストを実行します（Docker なし環境ではシナリオは Skipped）'
}

dotnet test $slnPath @filterArg @args
