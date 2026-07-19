#Requires -Version 7.0
<#
.SYNOPSIS
  Action Host コンテナに TestActionModule を載せ、CPU/Memory を段階的に下げて安定下限を測る。

.DESCRIPTION
  各候補値について複数試行し、成功率が閾値未満の値を「不安定」として除外する。
  成功条件（既定）:
  - 公開ポートへ TCP 到達
  - コンテナが OOMKilled でない
  - 試行ウィンドウ終了時点で Running
  - （任意）grpcurl で Echo Action が成功

.PARAMETER Image
  測定に使う Action Host イメージ。

.PARAMETER Trials
  候補値ごとの試行回数。

.PARAMETER StableSuccessRate
  この成功率以上を「安定」とみなす（0.0〜1.0）。

.PARAMETER MeasureMemory
  MemoryLimitMiB の段階測定を行う。

.PARAMETER MeasureCpu
  CpuLimit の段階測定を行う。

.PARAMETER MeasureTimeout
  コールドスタート〜 Echo 完了までの壁時計秒を、Timeout 候補と比較する。

.PARAMETER MemoryCandidatesMiB
  メモリ候補（降順推奨）。

.PARAMETER CpuCandidates
  CPU 候補（降順推奨）。

.PARAMETER TimeoutCandidatesSeconds
  Timeout 候補秒（降順推奨）。候補 T は「起動〜Echo が T 秒以内」で成功とみなす。

.PARAMETER ReadyTimeoutSeconds
  TCP 待受までの上限秒。

.PARAMETER SettleSeconds
  TCP 到達後、安定確認のために待つ秒数（Timeout 測定では使わない）。

.PARAMETER SkipGrpc
  grpcurl による ExecuteAction を省略し、起動安定性のみ測る（Timeout 測定では不可）。
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string] $Image = "statevia-action-host:local",
    [Parameter()]
    [int] $Trials = 5,
    [Parameter()]
    [double] $StableSuccessRate = 1.0,
    [Parameter()]
    [switch] $MeasureMemory,
    [Parameter()]
    [switch] $MeasureCpu,
    [Parameter()]
    [switch] $MeasureTimeout,
    # -File 経由の int[] 位置バインド事故を避けるため CSV 文字列で受け取る
    [Parameter()]
    [string] $MemoryCandidatesMiB = "512,384,256,192,128,96,64",
    [Parameter()]
    [string] $CpuCandidates = "1.0,0.5,0.25,0.1",
    [Parameter()]
    [string] $TimeoutCandidatesSeconds = "30,20,15,10,5,3,2,1",
    [Parameter()]
    [int] $ReadyTimeoutSeconds = 45,
    [Parameter()]
    [int] $SettleSeconds = 3,
    [Parameter()]
    [switch] $SkipGrpc,
    [Parameter()]
    [string] $TenantKey = "default",
    [Parameter()]
    [string] $GrpcImage = "fullstorydev/grpcurl:latest"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $MeasureMemory -and -not $MeasureCpu -and -not $MeasureTimeout) {
    $MeasureMemory = $true
    $MeasureCpu = $true
}

if ($MeasureTimeout -and $SkipGrpc) {
    throw "MeasureTimeout requires gRPC Echo; do not combine with -SkipGrpc."
}

$memoryLadder = @(
    $MemoryCandidatesMiB.Split(",", [System.StringSplitOptions]::RemoveEmptyEntries) |
        ForEach-Object { [int]($_).Trim() }
)
$cpuLadder = @(
    $CpuCandidates.Split(",", [System.StringSplitOptions]::RemoveEmptyEntries) |
        ForEach-Object { [double]($_).Trim() }
)
$timeoutLadder = @(
    $TimeoutCandidatesSeconds.Split(",", [System.StringSplitOptions]::RemoveEmptyEntries) |
        ForEach-Object { [int]($_).Trim() }
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$moduleProject = Join-Path $repoRoot "service/api/Statevia.Service.Api.Tests/Fixtures/TestActionModule/TestActionModule.csproj"
$workRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("statevia-sandbox-floor-" + [guid]::NewGuid().ToString("N"))
$modulesRoot = Join-Path $workRoot "modules"
# Action Host の discover はテナント配下ではなく modules 直下の module ディレクトリを見る
$moduleDir = Join-Path $modulesRoot "TestActionModule"
$protoPath = Join-Path $repoRoot "infrastructure/Statevia.Infrastructure.Actions.Grpc/Protos/action_execution.proto"
$results = [System.Collections.Generic.List[object]]::new()

function Write-Step([string] $Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Ensure-TestModuleLayout {
    Write-Step "Build TestActionModule and stage under $moduleDir"
    New-Item -ItemType Directory -Force -Path $moduleDir | Out-Null
    & dotnet build $moduleProject -c Release --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "TestActionModule build failed."
    }

    $outDir = Join-Path $repoRoot "service/api/Statevia.Service.Api.Tests/Fixtures/TestActionModule/bin/Release/net8.0"
    $entryDll = Join-Path $outDir "TestActionModule.dll"
    if (-not (Test-Path $entryDll)) {
        throw "Built assembly not found: $entryDll"
    }

    # Action Host テストの TestModuleLayout と同様、依存 DLL も同ディレクトリへ載せる
    Get-ChildItem -Path $outDir -Filter "*.dll" | ForEach-Object {
        Copy-Item -Force $_.FullName $moduleDir
    }
    $deps = Join-Path $outDir "TestActionModule.deps.json"
    if (Test-Path $deps) {
        Copy-Item -Force $deps $moduleDir
    }
}

function Test-TcpReady([string] $HostName, [int] $Port, [int] $TimeoutSeconds) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $client = [System.Net.Sockets.TcpClient]::new()
            $iar = $client.BeginConnect($HostName, $Port, $null, $null)
            $ok = $iar.AsyncWaitHandle.WaitOne(500)
            if ($ok -and $client.Connected) {
                $client.EndConnect($iar)
                $client.Dispose()
                return $true
            }
            $client.Dispose()
        }
        catch {
            # retry
        }
        Start-Sleep -Milliseconds 250
    }
    return $false
}

function Invoke-EchoGrpc([int] $Port) {
    # reflection 未実装のため proto を明示指定する。
    # Docker 上の grpcurl は `-d @/path` を解釈しないため、JSON はインラインで渡す。
    $payload = '{"executionId":"floor-measure","stateName":"Echo","actionId":"test.module.echo","tenantId":"00000000-0000-0000-0000-000000000001","inputJson":"\"ping\"","correlationId":"floor-measure"}'

    $protoDir = Split-Path -Parent $protoPath
    $protoFile = Split-Path -Leaf $protoPath
    $dockerArgs = @(
        "run", "--rm",
        "--add-host=host.docker.internal:host-gateway",
        "-v", "${protoDir}:/protos:ro",
        $GrpcImage,
        "-plaintext",
        "-import-path", "/protos",
        "-proto", $protoFile,
        "-d", $payload,
        "host.docker.internal:$Port",
        "statevia.actions.v1.ActionExecutionService/ExecuteAction"
    )

    $output = & docker @dockerArgs 2>&1
    $exit = $LASTEXITCODE
    $text = ($output | Out-String)
    if ($exit -ne 0) {
        return @{ Ok = $false; Detail = $text }
    }

    # proto3 JSON は camelCase。成功時は success: true
    if ($text -match '"success"\s*:\s*true') {
        return @{ Ok = $true; Detail = $text }
    }

    return @{ Ok = $false; Detail = $text }
}

function Start-MeasuredContainer {
    param(
        [object] $MemoryMiB = $null,
        [object] $Cpu = $null
    )

    $name = "sv-floor-" + [guid]::NewGuid().ToString("N").Substring(0, 12)
    $args = [System.Collections.Generic.List[string]]::new()
    foreach ($a in @("run", "-d", "--name", $name)) { $args.Add($a) }
    if ($null -ne $MemoryMiB) {
        $mem = [int]$MemoryMiB
        foreach ($a in @("--memory", "${mem}m", "--memory-swap", "${mem}m")) { $args.Add($a) }
    }
    if ($null -ne $Cpu) {
        $cpuValue = [double]$Cpu
        foreach ($a in @("--cpus", ("{0:0.###}" -f $cpuValue))) { $args.Add($a) }
    }
    foreach ($a in @(
            "-e", "ASPNETCORE_URLS=http://+:5001",
            "-e", "STATEVIA_MODULES_PATH=/app/modules",
            "-p", "127.0.0.1::5001",
            "-v", "${modulesRoot}:/app/modules:ro",
            $Image
        )) { $args.Add($a) }

    $null = & docker @($args.ToArray())
    if ($LASTEXITCODE -ne 0) {
        return @{ Name = $name; Ok = $false; Reason = "docker-run-failed" }
    }

    return @{ Name = $name; Ok = $true }
}

function Get-PublishedPort([string] $ContainerName) {
    $raw = & docker port $ContainerName 5001/tcp
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($raw)) {
        return $null
    }
    # e.g. 127.0.0.1:54321
    $part = ($raw | Select-Object -First 1).ToString().Trim()
    if ($part -match ":(\d+)$") {
        return [int]$Matches[1]
    }
    return $null
}

function Stop-MeasuredContainer([string] $Name) {
    & docker rm -f $Name 2>$null | Out-Null
}

function Invoke-ResourceTrial {
    param(
        [object] $MemoryMiB = $null,
        [object] $Cpu = $null,
        [bool] $UseGrpc
    )

    $started = Start-MeasuredContainer -MemoryMiB $MemoryMiB -Cpu $Cpu
    if (-not $started.Ok) {
        return @{ Success = $false; Reason = $started.Reason }
    }

    $name = $started.Name
    try {
        $port = $null
        $deadline = [DateTime]::UtcNow.AddSeconds($ReadyTimeoutSeconds)
        while ([DateTime]::UtcNow -lt $deadline -and -not $port) {
            $port = Get-PublishedPort -ContainerName $name
            if (-not $port) { Start-Sleep -Milliseconds 200 }
        }
        if (-not $port) {
            return @{ Success = $false; Reason = "port-not-published" }
        }

        if (-not (Test-TcpReady -HostName "127.0.0.1" -Port $port -TimeoutSeconds $ReadyTimeoutSeconds)) {
            $inspect = & docker inspect $name --format "{{.State.OOMKilled}}|{{.State.Status}}|{{.State.ExitCode}}" 2>$null
            return @{ Success = $false; Reason = "tcp-not-ready"; Inspect = "$inspect" }
        }

        Start-Sleep -Seconds $SettleSeconds

        $inspect = (& docker inspect $name --format "{{.State.OOMKilled}}|{{.State.Running}}|{{.State.ExitCode}}" 2>$null).ToString()
        $parts = $inspect -split "\|"
        $oom = $parts[0] -eq "true"
        $running = $parts[1] -eq "true"
        if ($oom) {
            return @{ Success = $false; Reason = "oom-killed" }
        }
        if (-not $running) {
            return @{ Success = $false; Reason = "not-running"; Inspect = $inspect }
        }

        if ($UseGrpc) {
            $grpc = Invoke-EchoGrpc -Port $port
            if (-not $grpc.Ok) {
                return @{ Success = $false; Reason = "grpc-failed"; Detail = $grpc.Detail }
            }
        }

        return @{ Success = $true; Reason = "ok"; Port = $port }
    }
    finally {
        Stop-MeasuredContainer -Name $name
    }
}

function Invoke-TimeoutBudgetTrial {
    param(
        [int] $BudgetSeconds,
        [int] $MemoryMiB,
        [double] $Cpu
    )

    # DockerSandboxRuntime の CancelAfter に相当: 起動〜 Echo 完了が Budget 秒以内か
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $started = Start-MeasuredContainer -MemoryMiB $MemoryMiB -Cpu $Cpu
    if (-not $started.Ok) {
        return @{ Success = $false; Reason = $started.Reason; ElapsedSeconds = $sw.Elapsed.TotalSeconds }
    }

    $name = $started.Name
    try {
        $port = $null
        $deadline = [DateTime]::UtcNow.AddSeconds([Math]::Min($ReadyTimeoutSeconds, $BudgetSeconds))
        while ([DateTime]::UtcNow -lt $deadline -and -not $port) {
            $port = Get-PublishedPort -ContainerName $name
            if (-not $port) { Start-Sleep -Milliseconds 100 }
        }
        if (-not $port) {
            return @{ Success = $false; Reason = "port-not-published"; ElapsedSeconds = $sw.Elapsed.TotalSeconds }
        }

        $tcpBudget = [Math]::Max(1, $BudgetSeconds - [int][Math]::Ceiling($sw.Elapsed.TotalSeconds))
        if (-not (Test-TcpReady -HostName "127.0.0.1" -Port $port -TimeoutSeconds $tcpBudget)) {
            return @{ Success = $false; Reason = "tcp-not-ready"; ElapsedSeconds = $sw.Elapsed.TotalSeconds }
        }

        if ($sw.Elapsed.TotalSeconds -gt $BudgetSeconds) {
            return @{ Success = $false; Reason = "budget-exceeded-before-grpc"; ElapsedSeconds = $sw.Elapsed.TotalSeconds }
        }

        $grpc = Invoke-EchoGrpc -Port $port
        $elapsed = $sw.Elapsed.TotalSeconds
        if (-not $grpc.Ok) {
            return @{ Success = $false; Reason = "grpc-failed"; ElapsedSeconds = $elapsed; Detail = $grpc.Detail }
        }

        if ($elapsed -gt $BudgetSeconds) {
            return @{ Success = $false; Reason = "budget-exceeded"; ElapsedSeconds = $elapsed }
        }

        return @{ Success = $true; Reason = "ok"; ElapsedSeconds = $elapsed; Port = $port }
    }
    finally {
        Stop-MeasuredContainer -Name $name
    }
}

function Measure-Ladder {
    param(
        [string] $Kind,
        [object[]] $Candidates,
        [scriptblock] $TrialFactory
    )

    Write-Step "Measure $Kind ladder: $($Candidates -join ', ') (trials=$Trials, stable>=$StableSuccessRate)"
    $recommended = $null

    foreach ($candidate in $Candidates) {
        $successes = 0
        $reasons = [System.Collections.Generic.List[string]]::new()
        for ($i = 1; $i -le $Trials; $i++) {
            Write-Host ("  [{0}] trial {1}/{2} ..." -f $candidate, $i, $Trials)
            $trial = & $TrialFactory $candidate
            if ($trial.Success) {
                $successes++
            }
            else {
                $reasons.Add($trial.Reason)
            }
        }

        $rate = $successes / [double]$Trials
        $stable = $rate -ge $StableSuccessRate
        $row = [pscustomobject]@{
            Kind       = $Kind
            Candidate  = "$candidate"
            Successes  = $successes
            Trials     = $Trials
            Rate       = [math]::Round($rate, 3)
            Stable     = $stable
            FailReasons = (($reasons | Group-Object | ForEach-Object { "$($_.Name)x$($_.Count)" }) -join ", ")
        }
        $results.Add($row)
        Write-Host ("  => rate={0:P0} stable={1} fails={2}" -f $rate, $stable, $row.FailReasons)

        if ($stable) {
            $recommended = $candidate
        }
        else {
            # 降順ラダーなので、不安定になったらそれより厳しい値は参考のみ継続
        }
    }

    return $recommended
}

try {
    Write-Step "Work dir: $workRoot"
    New-Item -ItemType Directory -Force -Path $workRoot | Out-Null
    Ensure-TestModuleLayout

    $imageCheck = & docker image inspect $Image 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Image not found: $Image. Build action-host image first."
    }

    $useGrpc = -not $SkipGrpc
    if ($useGrpc) {
        Write-Step "Pull grpcurl image if needed: $GrpcImage"
        & docker pull $GrpcImage | Out-Null
    }

    $memFloor = $null
    $cpuFloor = $null
    $timeoutFloor = $null
    $timeoutElapsedSamples = [System.Collections.Generic.List[double]]::new()

    if ($MeasureMemory) {
        $memFloor = Measure-Ladder -Kind "MemoryMiB" -Candidates $memoryLadder -TrialFactory {
            param($m)
            Invoke-ResourceTrial -MemoryMiB ([int]$m) -UseGrpc:$useGrpc
        }
    }

    if ($MeasureCpu) {
        # CPU 測定時はメモリを余裕ある値に固定（メモリ不足の影響を分離）
        $cpuMemoryMiB = if ($memFloor) { [Math]::Max([int]$memFloor * 2, 512) } else { 512 }
        Write-Step "CPU trials use fixed Memory=${cpuMemoryMiB}MiB"
        $cpuFloor = Measure-Ladder -Kind "Cpu" -Candidates $cpuLadder -TrialFactory {
            param($c)
            Invoke-ResourceTrial -MemoryMiB $cpuMemoryMiB -Cpu ([double]$c) -UseGrpc:$useGrpc
        }
    }

    if ($MeasureTimeout) {
        # Timeout 測定は運用下限寄り（64MiB / 0.25CPU）でコールドスタート＋Echo
        $timeoutMemoryMiB = 128
        $timeoutCpu = 0.25
        Write-Step "Timeout trials use fixed Memory=${timeoutMemoryMiB}MiB Cpu=$timeoutCpu (cold start + Echo, no settle)"
        $timeoutFloor = Measure-Ladder -Kind "TimeoutSeconds" -Candidates $timeoutLadder -TrialFactory {
            param($t)
            $trial = Invoke-TimeoutBudgetTrial -BudgetSeconds ([int]$t) -MemoryMiB $timeoutMemoryMiB -Cpu $timeoutCpu
            if ($null -ne $trial.ElapsedSeconds) {
                [void]$timeoutElapsedSamples.Add([double]$trial.ElapsedSeconds)
            }
            Write-Host ("      elapsed={0:N2}s reason={1}" -f $trial.ElapsedSeconds, $trial.Reason)
            $trial
        }
    }

    Write-Host ""
    Write-Step "Results"
    $results | Format-Table -AutoSize | Out-String | Write-Host

    Write-Step "Recommended stable floors (highest success on descending ladder that stayed stable)"
    # 降順で最後に Stable だった値 = 最も厳しい（小さい）安定値
    $memStable = @($results | Where-Object { $_.Kind -eq "MemoryMiB" -and $_.Stable })
    $cpuStable = @($results | Where-Object { $_.Kind -eq "Cpu" -and $_.Stable })
    $timeoutStable = @($results | Where-Object { $_.Kind -eq "TimeoutSeconds" -and $_.Stable })
    $memRec = if ($memStable.Count -gt 0) { $memStable[-1].Candidate } else { "(none)" }
    $cpuRec = if ($cpuStable.Count -gt 0) { $cpuStable[-1].Candidate } else { "(none)" }
    $timeoutRec = if ($timeoutStable.Count -gt 0) { $timeoutStable[-1].Candidate } else { "(none)" }

    Write-Host "MemoryLimitMiB floor: $memRec"
    Write-Host "CpuLimit floor:       $cpuRec"
    Write-Host "TimeoutSeconds floor: $timeoutRec"
    if ($timeoutElapsedSamples.Count -gt 0) {
        $sorted = $timeoutElapsedSamples | Sort-Object
        $p50 = $sorted[[int][Math]::Floor(($sorted.Count - 1) * 0.5)]
        $p95 = $sorted[[int][Math]::Floor(($sorted.Count - 1) * 0.95)]
        Write-Host ("Timeout elapsed samples: n={0} min={1:N2}s p50={2:N2}s p95={3:N2}s max={4:N2}s" -f `
            $sorted.Count, $sorted[0], $p50, $p95, $sorted[-1])
    }

    $csv = Join-Path $workRoot "results.csv"
    $results | Export-Csv -NoTypeInformation -Path $csv -Encoding utf8
    Write-Host "CSV: $csv"
}
finally {
    # 作業ディレクトリは結果確認のため残す（CSV パスを出力済み）
}
