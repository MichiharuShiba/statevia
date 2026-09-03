#Requires -Version 5.1
<#
.SYNOPSIS
  リポジトリ内の全 .NET ソリューションに、インデント・空白・不要な using のクリーンアップを適用する。

.DESCRIPTION
  スクリプト配置（リポジトリの sonar/）から各 sln のパスを解決する。
  カレントディレクトリに依存しない。
  `whitespace` と `style --diagnostics IDE0005 IDE0065` を順に実行し、
  インデント・空白・不要な using の削除、using 配置の整理に限定する。

  対象:
  - core/engine/statevia-engine.sln
  - service/api/statevia-api.sln
  - service/action-host/statevia-action-host.sln
  - service/cli/statevia-cli.sln
  - infrastructure/statevia-infrastructure.sln

.PARAMETER VerifyNoChanges
  書式変更が発生しないことを検証する（`dotnet format --verify-no-changes`）。
  CI や PR 前チェック向け。

.PARAMETER Verbosity
  `dotnet format` の詳細レベル（quiet / minimal / normal / detailed / diagnostic）。

.PARAMETER Solution
  対象 sln の識別子（`engine` / `api` / `action-host` / `cli` / `infrastructure`）、
  相対パス、またはファイル名（例: statevia-api.sln）。未指定時は全ソリューション。

.EXAMPLE
  .\sonar\dotnet-format-all.ps1

.EXAMPLE
  .\sonar\dotnet-format-all.ps1 -VerifyNoChanges

.EXAMPLE
  .\sonar\dotnet-format-all.ps1 -Solution statevia-api.sln
#>
[CmdletBinding()]
param(
    [switch]$VerifyNoChanges,
    [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
    [string]$Verbosity = 'normal',
    [string]$Solution
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

$allSolutions = @(
    @{ Name = 'engine'; RelativePath = 'core\engine\statevia-engine.sln' }
    @{ Name = 'api'; RelativePath = 'service\api\statevia-api.sln' }
    @{ Name = 'action-host'; RelativePath = 'service\action-host\statevia-action-host.sln' }
    @{ Name = 'cli'; RelativePath = 'service\cli\statevia-cli.sln' }
    @{ Name = 'infrastructure'; RelativePath = 'infrastructure\statevia-infrastructure.sln' }
)

$targets = if ([string]::IsNullOrWhiteSpace($Solution)) {
    $allSolutions
} else {
    $matched = $allSolutions | Where-Object {
        $_.Name -eq $Solution -or
        $_.RelativePath -eq $Solution -or
        [System.IO.Path]::GetFileName($_.RelativePath) -eq $Solution
    }
    if (-not $matched) {
        $available = ($allSolutions | ForEach-Object { [System.IO.Path]::GetFileName($_.RelativePath) }) -join ', '
        Write-Error "ソリューション '$Solution' が見つかりません。利用可能: $available"
        exit 1
    }

    @($matched)
}

Write-Host "dotnet format: $($targets.Count) solution(s)" -ForegroundColor Cyan
Write-Host "Repo: $repoRoot"
if ($VerifyNoChanges) {
    Write-Host 'Mode: verify-no-changes' -ForegroundColor Yellow
}

$failed = [System.Collections.Generic.List[string]]::new()

foreach ($target in $targets) {
    $solutionPath = Join-Path $repoRoot $target.RelativePath
    if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
        $failed.Add("$($target.Name): not found ($solutionPath)")
        continue
    }

    Write-Host ""
    Write-Host "=== $($target.Name) ($($target.RelativePath)) ===" -ForegroundColor Cyan

    $formatCommands = @(
        @{
            Name = 'whitespace'
            Args = @('format', 'whitespace', $solutionPath, '--verbosity', $Verbosity)
        }
        @{
            Name = 'using-cleanup'
            Args = @('format', 'style', $solutionPath, '--diagnostics', 'IDE0005', 'IDE0065', '--severity', 'info', '--verbosity', $Verbosity)
        }
    )

    if ($VerifyNoChanges) {
        foreach ($formatCommand in $formatCommands) {
            $formatCommand.Args += '--verify-no-changes'
        }
    }

    $targetFailed = $false
    foreach ($formatCommand in $formatCommands) {
        Write-Host ("--- {0}" -f $formatCommand.Name) -ForegroundColor DarkCyan
        & dotnet @($formatCommand.Args)
        if ($LASTEXITCODE -ne 0) {
            $targetFailed = $true
            break
        }
    }

    if ($targetFailed) {
        $failed.Add($target.Name)
    }
}

Write-Host ""
if ($failed.Count -gt 0) {
    Write-Error ("dotnet format failed: {0}" -f ($failed -join ', '))
    exit 1
}

Write-Host 'dotnet format completed for all solutions.' -ForegroundColor Green
