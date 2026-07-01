#Requires -Version 5.1
<#
.SYNOPSIS
  engine / api / ui の行数をプロダクト・テスト別に集計する。

.DESCRIPTION
  物理行数（空行・コメント含む）と実効コード行数（空行・コメント除く）を出力する。
  リポジトリの sonar/ から相対パスを解決するため、カレントディレクトリに依存しない。

.PARAMETER Detailed
  サブプロジェクト単位・言語別・api Migrations / ui e2e などの内訳を表示する。

.PARAMETER Json
  集計結果を JSON で標準出力する（テーブル出力は行わない）。

.EXAMPLE
  .\sonar\measure-loc.ps1

.EXAMPLE
  .\sonar\measure-loc.ps1 -Detailed

.EXAMPLE
  .\sonar\measure-loc.ps1 -Json | Set-Content loc-report.json
#>
[CmdletBinding()]
param(
    [switch]$Detailed,
    [switch]$Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot

$ExcludeDirPattern = '\\(bin|obj|node_modules|\.next|coverage)\\'

function Test-ExcludedPath {
    param([string]$FullPath)
    return $FullPath -match $ExcludeDirPattern
}

function Get-LanguageFromExtension {
    param([string]$Extension)
    switch ($Extension.ToLowerInvariant()) {
        '.cs' { return 'cs' }
        '.ts' { return 'ts' }
        '.tsx' { return 'tsx' }
        default { return $Extension.TrimStart('.').ToLowerInvariant() }
    }
}

function Test-UiTestPath {
    param([string]$FullPath)
    return (
        $FullPath -match '\\tests\\' -or
        $FullPath -match '\.test\.(ts|tsx)$' -or
        $FullPath -match '\.spec\.(ts|tsx)$' -or
        $FullPath -match '\\e2e\\'
    )
}

function Test-UiE2ePath {
    param([string]$FullPath)
    return $FullPath -match '\\e2e\\'
}

function Get-EffectiveLineKind {
    param(
        [string]$Line,
        [ref]$InBlockComment,
        [ValidateSet('csharp', 'typescript')]
        [string]$Language
    )

    if ([string]::IsNullOrWhiteSpace($Line)) {
        return 'Blank'
    }

    $remaining = $Line
    $hasCode = $false

    while ($remaining.Length -gt 0) {
        if ($InBlockComment.Value) {
            $endIndex = $remaining.IndexOf('*/')
            if ($endIndex -lt 0) {
                return 'Comment'
            }

            $InBlockComment.Value = $false
            $remaining = $remaining.Substring($endIndex + 2)
            continue
        }

        $blockStart = $remaining.IndexOf('/*')
        $lineComment = $remaining.IndexOf('//')

        if ($blockStart -ge 0 -and ($lineComment -lt 0 -or $blockStart -lt $lineComment)) {
            $before = $remaining.Substring(0, $blockStart)
            if (-not [string]::IsNullOrWhiteSpace($before)) {
                $hasCode = $true
            }

            $afterStart = $remaining.Substring($blockStart + 2)
            $blockEnd = $afterStart.IndexOf('*/')
            if ($blockEnd -lt 0) {
                $InBlockComment.Value = $true
                if ($hasCode) {
                    return 'Code'
                }

                return 'Comment'
            }

            $remaining = $afterStart.Substring($blockEnd + 2)
            continue
        }

        if ($lineComment -ge 0) {
            $before = $remaining.Substring(0, $lineComment)
            if (-not [string]::IsNullOrWhiteSpace($before)) {
                $hasCode = $true
            }

            return $(if ($hasCode) { 'Code' } else { 'Comment' })
        }

        if (-not [string]::IsNullOrWhiteSpace($remaining)) {
            $hasCode = $true
        }

        break
    }

    if ($hasCode) {
        return 'Code'
    }

    return 'Comment'
}

function New-LineStats {
    return [ordered]@{
        Files    = 0
        Physical = 0
        Blank    = 0
        Comment  = 0
        Code     = 0
    }
}

function Add-LineStats {
    param(
        $Target,
        $Source
    )

    foreach ($key in @('Files', 'Physical', 'Blank', 'Comment', 'Code')) {
        $Target[$key] += $Source[$key]
    }
}

function Measure-SourceFile {
    param(
        [System.IO.FileInfo]$File,
        [ValidateSet('csharp', 'typescript')]
        [string]$Language
    )

    $stats = New-LineStats
    $stats.Files = 1

    $inBlockComment = $false
    $lines = Get-Content -LiteralPath $File.FullName -Encoding UTF8

    foreach ($line in $lines) {
        $stats.Physical++
        $kind = Get-EffectiveLineKind -Line $line -InBlockComment ([ref]$inBlockComment) -Language $Language
        switch ($kind) {
            'Blank' { $stats.Blank++ }
            'Comment' { $stats.Comment++ }
            'Code' { $stats.Code++ }
        }
    }

    return $stats
}

function Get-SourceFiles {
    param(
        [string]$RootPath,
        [string[]]$Extensions
    )

    if (-not (Test-Path -LiteralPath $RootPath -PathType Container)) {
        return @()
    }

    return Get-ChildItem -LiteralPath $RootPath -Recurse -File |
        Where-Object {
            $_.Extension -in $Extensions -and -not (Test-ExcludedPath $_.FullName)
        }
}

function Measure-PathGroup {
    param(
        [string]$RootPath,
        [string[]]$Extensions,
        [ValidateSet('csharp', 'typescript')]
        [string]$Language,
        [scriptblock]$IncludeFile = { $true }
    )

    $total = New-LineStats
    $byLanguage = @{}

    foreach ($file in (Get-SourceFiles -RootPath $RootPath -Extensions $Extensions)) {
        if (-not (& $IncludeFile $file.FullName)) {
            continue
        }

        $fileStats = Measure-SourceFile -File $file -Language $Language
        Add-LineStats -Target $total -Source $fileStats

        $lang = Get-LanguageFromExtension $file.Extension
        if (-not $byLanguage.ContainsKey($lang)) {
            $byLanguage[$lang] = New-LineStats
        }

        Add-LineStats -Target $byLanguage[$lang] -Source $fileStats
    }

    return [PSCustomObject]@{
        Stats      = $total
        ByLanguage = $byLanguage
    }
}

function Format-CountCell {
    param(
        [int]$Physical,
        [int]$Code
    )

    return '{0} ({1})' -f $Physical, $Code
}

function Write-SummaryTable {
    param([array]$Rows)

    $header = '{0,-12} {1,-12} {2,14} {3,14} {4,8} {5,14} {6,14}' -f `
        'Component', 'Category', 'Physical', 'Code', 'Files', 'TestRatio', 'CodeRatio'
    Write-Output $header
    Write-Output ('-' * $header.Length)

    foreach ($row in $Rows) {
        $testRatio = if ($row.Category -eq 'test' -and $row.ProductPhysical -gt 0) {
            '{0:P0}' -f ($row.Physical / $row.ProductPhysical)
        } elseif ($row.Category -eq 'product') {
            '-'
        } else {
            'n/a'
        }

        $codeRatio = if ($row.Physical -gt 0) {
            '{0:P0}' -f ($row.Code / $row.Physical)
        } else {
            'n/a'
        }

        Write-Output ('{0,-12} {1,-12} {2,14} {3,14} {4,8} {5,14} {6,14}' -f `
            $row.Component,
            $row.Category,
            $row.Physical,
            $row.Code,
            $row.Files,
            $testRatio,
            $codeRatio)
    }
}

$engineProductDefs = @(
    @{ Name = 'Statevia.Core.Engine'; RelativePath = 'core\engine\Statevia.Core.Engine' }
    @{ Name = 'Statevia.Service.Cli'; RelativePath = 'core\engine\Statevia.Service.Cli' }
)

$engineTestDefs = @(
    @{ Name = 'Statevia.Core.Engine.Tests'; RelativePath = 'core\engine\Statevia.Core.Engine.Tests' }
    @{ Name = 'Statevia.Service.Cli.Tests'; RelativePath = 'core\engine\Statevia.Service.Cli.Tests' }
)

$apiProductDefs = @(
    @{ Name = 'Statevia.Service.Api'; RelativePath = 'service\api\Statevia.Service.Api' }
    @{ Name = 'Statevia.Service.Api.Bootstrap'; RelativePath = 'service\api\Statevia.Service.Api.Bootstrap' }
)

$apiTestDefs = @(
    @{ Name = 'Statevia.Service.Api.Tests'; RelativePath = 'service\api\Statevia.Service.Api.Tests' }
)

function Measure-DefinitionGroup {
    param(
        [array]$Definitions,
        [string[]]$Extensions,
        [ValidateSet('csharp', 'typescript')]
        [string]$Language,
        [scriptblock]$IncludeFile = { $true }
    )

    $groupStats = New-LineStats
    $byLanguage = @{}
    $details = [System.Collections.Generic.List[object]]::new()

    foreach ($definition in $Definitions) {
        $absolutePath = Join-Path $RepoRoot $definition.RelativePath
        $measured = Measure-PathGroup -RootPath $absolutePath -Extensions $Extensions -Language $Language -IncludeFile $IncludeFile
        Add-LineStats -Target $groupStats -Source $measured.Stats

        foreach ($lang in $measured.ByLanguage.Keys) {
            if (-not $byLanguage.ContainsKey($lang)) {
                $byLanguage[$lang] = New-LineStats
            }

            Add-LineStats -Target $byLanguage[$lang] -Source $measured.ByLanguage[$lang]
        }

        $details.Add([PSCustomObject]@{
            Name       = $definition.Name
            Path       = $definition.RelativePath
            Physical   = $measured.Stats.Physical
            Code       = $measured.Stats.Code
            Blank      = $measured.Stats.Blank
            Comment    = $measured.Stats.Comment
            Files      = $measured.Stats.Files
            ByLanguage = $measured.ByLanguage
        })
    }

    return [PSCustomObject]@{
        Stats   = $groupStats
        Details = $details
        ByLanguage = $byLanguage
    }
}

$engineProduct = Measure-DefinitionGroup -Definitions $engineProductDefs -Extensions @('.cs') -Language 'csharp'
$engineTest = Measure-DefinitionGroup -Definitions $engineTestDefs -Extensions @('.cs') -Language 'csharp'
$apiProduct = Measure-DefinitionGroup -Definitions $apiProductDefs -Extensions @('.cs') -Language 'csharp'
$apiTest = Measure-DefinitionGroup -Definitions $apiTestDefs -Extensions @('.cs') -Language 'csharp'

$uiRoot = Join-Path $RepoRoot 'ui\studio'
$uiProduct = Measure-PathGroup -RootPath $uiRoot -Extensions @('.ts', '.tsx') -Language 'typescript' -IncludeFile {
    param($fullPath)
    -not (Test-UiTestPath $fullPath)
}
$uiTest = Measure-PathGroup -RootPath $uiRoot -Extensions @('.ts', '.tsx') -Language 'typescript' -IncludeFile {
    param($fullPath)
    Test-UiTestPath $fullPath
}
$uiE2e = Measure-PathGroup -RootPath $uiRoot -Extensions @('.ts', '.tsx') -Language 'typescript' -IncludeFile {
    param($fullPath)
    Test-UiE2ePath $fullPath
}
$uiUnitTestStats = New-LineStats
Add-LineStats -Target $uiUnitTestStats -Source $uiTest.Stats
foreach ($key in @('Files', 'Physical', 'Blank', 'Comment', 'Code')) {
    $uiUnitTestStats[$key] -= $uiE2e.Stats[$key]
}

$apiMigrationsPath = Join-Path $RepoRoot 'service\api\Statevia.Service.Api\Migrations'
$apiMigrations = Measure-PathGroup -RootPath $apiMigrationsPath -Extensions @('.cs') -Language 'csharp'

$apiProductWithoutMigrations = New-LineStats
Add-LineStats -Target $apiProductWithoutMigrations -Source $apiProduct.Stats
$apiProductWithoutMigrations.Physical -= $apiMigrations.Stats.Physical
$apiProductWithoutMigrations.Code -= $apiMigrations.Stats.Code
$apiProductWithoutMigrations.Blank -= $apiMigrations.Stats.Blank
$apiProductWithoutMigrations.Comment -= $apiMigrations.Stats.Comment
$apiProductWithoutMigrations.Files -= $apiMigrations.Stats.Files

function New-ComponentSummary {
    param(
        [string]$Component,
        [string]$Category,
        $Stats,
        [int]$ProductPhysical = 0
    )

    return [PSCustomObject]@{
        Component       = $Component
        Category        = $Category
        Physical        = $Stats.Physical
        Code            = $Stats.Code
        Blank           = $Stats.Blank
        Comment         = $Stats.Comment
        Files           = $Stats.Files
        ProductPhysical = $ProductPhysical
    }
}

$summaryRows = @(
    (New-ComponentSummary -Component 'engine' -Category 'product' -Stats $engineProduct.Stats -ProductPhysical $engineProduct.Stats.Physical)
    (New-ComponentSummary -Component 'engine' -Category 'test' -Stats $engineTest.Stats -ProductPhysical $engineProduct.Stats.Physical)
    (New-ComponentSummary -Component 'api' -Category 'product' -Stats $apiProduct.Stats -ProductPhysical $apiProduct.Stats.Physical)
    (New-ComponentSummary -Component 'api' -Category 'test' -Stats $apiTest.Stats -ProductPhysical $apiProduct.Stats.Physical)
    (New-ComponentSummary -Component 'ui' -Category 'product' -Stats $uiProduct.Stats -ProductPhysical $uiProduct.Stats.Physical)
    (New-ComponentSummary -Component 'ui' -Category 'test' -Stats $uiTest.Stats -ProductPhysical $uiProduct.Stats.Physical)
)

$grandProduct = New-LineStats
$grandTest = New-LineStats
Add-LineStats -Target $grandProduct -Source $engineProduct.Stats
Add-LineStats -Target $grandProduct -Source $apiProduct.Stats
Add-LineStats -Target $grandProduct -Source $uiProduct.Stats
Add-LineStats -Target $grandTest -Source $engineTest.Stats
Add-LineStats -Target $grandTest -Source $apiTest.Stats
Add-LineStats -Target $grandTest -Source $uiTest.Stats

$report = [ordered]@{
    GeneratedAt = (Get-Date).ToString('o')
    RepoRoot    = $RepoRoot
    Notes       = @(
        'Physical = 物理行数（空行・コメント含む）'
        'Code = 実効コード行数（空行・行コメント・ブロックコメントを除く）'
        'engine/samples は除外'
        'ui テスト = tests/, *.test.*, *.spec.*, e2e/'
        'コメント判定は文字列リテラル内のスラッシュ連続を完全には除外しない近似'
    )
    Summary = @{
        Rows = $summaryRows
        Totals = [ordered]@{
            Product = $grandProduct
            Test    = $grandTest
            All     = $({
                $all = New-LineStats
                Add-LineStats -Target $all -Source $grandProduct
                Add-LineStats -Target $all -Source $grandTest
                $all
            }.Invoke())
        }
    }
    Components = [ordered]@{
        engine = [ordered]@{
            product = $engineProduct
            test    = $engineTest
        }
        api    = [ordered]@{
            product = $apiProduct
            test    = $apiTest
            migrations = $apiMigrations.Stats
            productWithoutMigrations = $apiProductWithoutMigrations
        }
        ui     = [ordered]@{
            product  = $uiProduct
            test     = $uiTest
            unitTest = @{
                Stats = $uiUnitTestStats
                ByLanguage = $uiTest.ByLanguage
            }
            e2e      = $uiE2e
        }
    }
}

if ($Json) {
    $report | ConvertTo-Json -Depth 8
    return
}

Write-Output 'Statevia LOC report'
Write-Output ('Repo: {0}' -f $RepoRoot)
Write-Output ('Generated: {0}' -f $report.GeneratedAt)
Write-Output ''
Write-Output '=== Summary (Physical / Code) ==='
Write-SummaryTable -Rows $summaryRows
Write-Output ''
Write-Output ('Product total : Physical {0}, Code {1}, Files {2}' -f $grandProduct.Physical, $grandProduct.Code, $grandProduct.Files)
Write-Output ('Test total    : Physical {0}, Code {1}, Files {2}' -f $grandTest.Physical, $grandTest.Code, $grandTest.Files)
Write-Output ('Grand total   : Physical {0}, Code {1}, Files {2}' -f $report.Summary.Totals.All.Physical, $report.Summary.Totals.All.Code, $report.Summary.Totals.All.Files)

if (-not $Detailed) {
    Write-Output ''
    Write-Output '内訳は -Detailed、JSON 出力は -Json を指定してください。'
    return
}

Write-Output ''
Write-Output '=== engine 内訳 ==='
foreach ($detail in $engineProduct.Details + $engineTest.Details) {
    Write-Output ('{0}: Physical {1}, Code {2}, Files {3}' -f $detail.Name, $detail.Physical, $detail.Code, $detail.Files)
}

Write-Output ''
Write-Output '=== api 内訳 ==='
foreach ($detail in $apiProduct.Details + $apiTest.Details) {
    Write-Output ('{0}: Physical {1}, Code {2}, Files {3}' -f $detail.Name, $detail.Physical, $detail.Code, $detail.Files)
}

Write-Output ('Migrations: Physical {0}, Code {1}, Files {2}' -f $apiMigrations.Stats.Physical, $apiMigrations.Stats.Code, $apiMigrations.Stats.Files)
Write-Output ('Api product (Migrations 除く): Physical {0}, Code {1}, Files {2}' -f `
    $apiProductWithoutMigrations.Physical, $apiProductWithoutMigrations.Code, $apiProductWithoutMigrations.Files)

Write-Output ''
Write-Output '=== ui 内訳 ==='
Write-Output ('product: Physical {0}, Code {1}, Files {2}' -f $uiProduct.Stats.Physical, $uiProduct.Stats.Code, $uiProduct.Stats.Files)
Write-Output ('tests/ (unit): Physical {0}, Code {1}, Files {2}' -f $uiUnitTestStats.Physical, $uiUnitTestStats.Code, $uiUnitTestStats.Files)
Write-Output ('e2e/: Physical {0}, Code {1}, Files {2}' -f $uiE2e.Stats.Physical, $uiE2e.Stats.Code, $uiE2e.Stats.Files)

Write-Output ''
Write-Output '=== 言語別 (Physical / Code / Files) ==='
$languageRows = @(
    @{ Component = 'engine'; Category = 'product'; Map = $engineProduct.ByLanguage }
    @{ Component = 'engine'; Category = 'test'; Map = $engineTest.ByLanguage }
    @{ Component = 'api'; Category = 'product'; Map = $apiProduct.ByLanguage }
    @{ Component = 'api'; Category = 'test'; Map = $apiTest.ByLanguage }
    @{ Component = 'ui'; Category = 'product'; Map = $uiProduct.ByLanguage }
    @{ Component = 'ui'; Category = 'test'; Map = $uiTest.ByLanguage }
)

foreach ($languageRow in $languageRows) {
    foreach ($lang in ($languageRow.Map.Keys | Sort-Object)) {
        $stats = $languageRow.Map[$lang]
        Write-Output ('{0} / {1} / {2}: Physical {3}, Code {4}, Files {5}' -f `
            $languageRow.Component, $languageRow.Category, $lang, $stats.Physical, $stats.Code, $stats.Files)
    }
}

Write-Output ''
Write-Output '=== 注記 ==='
foreach ($note in $report.Notes) {
    Write-Output "- $note"
}
