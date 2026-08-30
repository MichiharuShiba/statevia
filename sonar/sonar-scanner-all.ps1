#Requires -Version 5.1
<#
.SYNOPSIS
  複数コンポーネントの SonarScanner を順に実行する。

.DESCRIPTION
  既存のコンポーネント別スクリプト（sonar-scanner-*.ps1）を呼び出すラッパー。
  -Projects 未指定時は登録済みの全プロジェクトを実行する。
  識別子は短い名前（engine / api 等）または SonarQube の projectKey を受け付ける。
  カレントディレクトリに依存しない。

.PARAMETER Projects
  実行するプロジェクト識別子。カンマ区切りまたは複数指定。未指定時は全件。

.PARAMETER List
  実行せず、利用可能な識別子と projectKey を表示する。

.PARAMETER SkipBuildServerShutdown
  先頭の `dotnet build-server shutdown` を省略する。

.EXAMPLE
  .\sonar\sonar-scanner-all.ps1

.EXAMPLE
  .\sonar\sonar-scanner-all.ps1 -Projects engine,api,ui

.EXAMPLE
  .\sonar\sonar-scanner-all.ps1 -Projects StateviaCoreEngine,StateviaServiceApi

.EXAMPLE
  .\sonar\sonar-scanner-all.ps1 -List
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [Alias('Project')]
    [string[]]$Projects,

    [switch]$List,

    [switch]$SkipBuildServerShutdown
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# SonarQube 上のキーと 1:1。追加時は個別スキャナと README の一覧も揃える。
$catalog = @(
    @{
        Name = 'engine'
        ProjectKey = 'StateviaCoreEngine'
        Script = 'sonar-scanner-engine.ps1'
        Aliases = @('engine', 'StateviaCoreEngine')
    }
    @{
        Name = 'api'
        ProjectKey = 'StateviaServiceApi'
        Script = 'sonar-scanner-api.ps1'
        Aliases = @('api', 'StateviaServiceApi')
    }
    @{
        Name = 'runtime'
        ProjectKey = 'StateviaServiceRuntime'
        Script = 'sonar-scanner-runtime.ps1'
        Aliases = @('runtime', 'StateviaServiceRuntime')
    }
    @{
        Name = 'cli'
        ProjectKey = 'StateviaServiceCLI'
        Script = 'sonar-scanner-cli.ps1'
        Aliases = @('cli', 'StateviaServiceCLI')
    }
    @{
        Name = 'action-host'
        ProjectKey = 'StateviaServiceActionHost'
        Script = 'sonar-scanner-action-host.ps1'
        Aliases = @('action-host', 'actionhost', 'StateviaServiceActionHost')
    }
    @{
        Name = 'reference'
        ProjectKey = 'StateviaModulesReference'
        Script = 'sonar-scanner-reference.ps1'
        Aliases = @('reference', 'StateviaModulesReference')
    }
    @{
        Name = 'ui'
        ProjectKey = 'StateviaUIStudio'
        Script = 'sonar-scanner-ui.ps1'
        Aliases = @('ui', 'studio', 'StateviaUIStudio')
    }
)

function Get-AvailableProjectHelp {
    $lines = $catalog | ForEach-Object {
        $aliasText = ($_.Aliases -join ', ')
        "  $($_.Name)  ($($_.ProjectKey))  aliases: $aliasText"
    }
    return (@('利用可能なプロジェクト:') + @($lines)) -join [Environment]::NewLine
}

function ConvertTo-ProjectTokens {
    param([string[]]$Raw)

    # 関数出力の列挙で List がほどけないよう、単一オブジェクトとして返す。
    $tokens = [System.Collections.Generic.List[string]]::new()
    if ($null -eq $Raw) {
        return ,$tokens
    }

    foreach ($item in $Raw) {
        foreach ($part in ($item -split ',')) {
            $trimmed = $part.Trim()
            if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
                $tokens.Add($trimmed)
            }
        }
    }

    return ,$tokens
}

function Resolve-SonarProjects {
    param([string[]]$Requested)

    $resolved = [System.Collections.Generic.List[object]]::new()
    if ($null -eq $Requested -or $Requested.Length -eq 0) {
        foreach ($item in $catalog) {
            $resolved.Add($item)
        }

        return ,$resolved
    }

    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $unknown = [System.Collections.Generic.List[string]]::new()

    foreach ($token in $Requested) {
        $matched = $catalog | Where-Object {
            $_.Name -eq $token -or
            $_.ProjectKey -eq $token -or
            ($_.Aliases | Where-Object { $_ -eq $token })
        } | Select-Object -First 1

        if ($null -eq $matched) {
            $unknown.Add($token)
            continue
        }

        if ($seen.Add($matched.Name)) {
            $resolved.Add($matched)
        }
    }

    if ($unknown.Count -gt 0) {
        $unknownText = $unknown -join ', '
        Write-Error ("未知のプロジェクトです: {0}`n{1}" -f $unknownText, (Get-AvailableProjectHelp))
        exit 1
    }

    return ,$resolved
}

if ($List) {
    Write-Host (Get-AvailableProjectHelp)
    exit 0
}

if (-not $env:SONAR_TOKEN) {
    Write-Error '環境変数 SONAR_TOKEN が設定されていません。'
    exit 1
}

$tokens = ConvertTo-ProjectTokens -Raw $Projects
$targets = Resolve-SonarProjects -Requested $tokens

Write-Host "SonarQube 分析: $($targets.Count) project(s)" -ForegroundColor Cyan
foreach ($target in $targets) {
    Write-Host "  - $($target.Name) ($($target.ProjectKey))"
}

if (-not $SkipBuildServerShutdown) {
    Write-Host ''
    Write-Host 'dotnet build-server shutdown' -ForegroundColor Yellow
    dotnet build-server shutdown
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] dotnet build-server shutdown failed'
        exit 1
    }
}

$hostExecutable = (Get-Process -Id $PID).Path
$failed = [System.Collections.Generic.List[string]]::new()
$succeeded = [System.Collections.Generic.List[string]]::new()

foreach ($target in $targets) {
    $scriptPath = Join-Path $PSScriptRoot $target.Script
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        $failed.Add("$($target.Name): script not found ($scriptPath)")
        continue
    }

    Write-Host ''
    Write-Host "=== $($target.Name) ($($target.ProjectKey)) ===" -ForegroundColor Cyan

    # 別プロセスで実行し、子の exit がこのスクリプトを終了しないようにする。
    # Start-Process -Wait は使わない。-NoNewWindow 時に sonar の Node / dotnet が
    # コンソールを握ったままだと、子スクリプト終了後も Wait が戻らない。
    $LASTEXITCODE = 0
    & $hostExecutable -NoProfile -File $scriptPath
    $exitCode = 0
    if ($null -ne $LASTEXITCODE) {
        $exitCode = $LASTEXITCODE
    }

    if ($exitCode -eq 0) {
        $succeeded.Add($target.Name)
        Write-Host ('Finished {0} (exit {1})' -f $target.Name, $exitCode) -ForegroundColor Green
    }
    else {
        $failed.Add($target.Name)
        Write-Host ('Finished {0} (exit {1})' -f $target.Name, $exitCode) -ForegroundColor Red
    }
}

Write-Host ''
Write-Host '=== Summary ===' -ForegroundColor Cyan
foreach ($name in $succeeded) {
    Write-Host ('[OK] {0}' -f $name) -ForegroundColor Green
}
foreach ($name in $failed) {
    Write-Host ('[FAIL] {0}' -f $name) -ForegroundColor Red
}

if ($failed.Count -gt 0) {
    Write-Error ("Sonar 分析が失敗しました: {0}" -f ($failed -join ', '))
    exit 1
}

Write-Host '[OK] SonarQube analysis completed for all requested projects.' -ForegroundColor Green
