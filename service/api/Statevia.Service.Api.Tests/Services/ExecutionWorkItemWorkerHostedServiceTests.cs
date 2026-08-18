using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Application.Contracts;
using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Core.Application.Contracts.Security;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Security;
using Statevia.Runtime.Services;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>ExecutionWorkItemWorkerHostedService の単体テスト（Runtime カバレッジ用）。</summary>
public sealed class ExecutionWorkItemWorkerHostedServiceTests
{
    /// <summary>空 claim のときポーリング待機へ入る。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenClaimEmpty_PollsWithoutProcessing()
    {
        // Arrange
        var queue = new FakeWorkerQueue();
        await using var provider = BuildProvider(queue, new RecordingExecutionService(), new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var sut = new ExecutionWorkItemWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator());

        // Act
        await sut.StartAsync(cts.Token);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(180), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // テスト側待機キャンセルは無視する。
        }

        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(queue.ClaimCallCount >= 1);
        Assert.Equal(0, queue.CompleteCallCount);
    }

    /// <summary>Start work item を claim すると ExecuteQueuedStart と Complete が呼ばれる。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenStartWorkItemClaimed_CompletesAfterQueuedStart()
    {
        // Arrange
        var executionId = Guid.Parse("10101010-1010-1010-1010-101010101010");
        var workItemId = Guid.Parse("20202020-2020-2020-2020-202020202020");
        using var inputDoc = JsonDocument.Parse("{}");
        var payload = JsonSerializer.Serialize(
            new ExecutionStartWorkItemPayload(
                executionId,
                new StartExecutionRequest { DefinitionId = "d1", Input = inputDoc.RootElement }),
            ExecutionWorkItemPayloadJson.Options);
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Start,
            Payload = payload,
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue { ItemsToClaim = [item] };
        var executions = new RecordingExecutionService { BeginGenerationToReturn = 3 };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new ExecutionWorkItemWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator());

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.CompleteCallCount > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, executions.ExecuteQueuedStartCalls);
        Assert.Equal(1, executions.BeginOwnedSessionCalls);
        Assert.Equal(1, executions.AwaitLocalLoadCalls);
        Assert.Equal(1, executions.EndOwnedSessionCalls);
        Assert.Equal(1, queue.CompleteCallCount);
    }

    /// <summary>テナントが無い work item は Complete してスキップする。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenTenantMissing_CompletesWithoutSession()
    {
        // Arrange
        var executionId = Guid.Parse("30303030-3030-3030-3030-303030303030");
        var workItemId = Guid.Parse("40404040-4040-4040-4040-404040404040");
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Cancel,
            Payload = "{}",
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue { ItemsToClaim = [item] };
        var executions = new RecordingExecutionService();
        await using var provider = BuildProvider(
            queue,
            executions,
            new StubPlatformDataAccess { TenantToReturn = null });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new ExecutionWorkItemWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator());

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.CompleteCallCount > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, queue.CompleteCallCount);
        Assert.Equal(0, executions.BeginOwnedSessionCalls);
        Assert.Equal(0, executions.CancelCalls);
    }

    /// <summary>ownership 獲得失敗時は Release する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenOwnershipAcquireFails_ReleasesWorkItem()
    {
        // Arrange
        var executionId = Guid.Parse("50505050-5050-5050-5050-505050505050");
        var workItemId = Guid.Parse("60606060-6060-6060-6060-606060606060");
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Cancel,
            Payload = "{}",
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue { ItemsToClaim = [item] };
        var executions = new RecordingExecutionService { BeginGenerationToReturn = null };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new ExecutionWorkItemWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator());

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.ReleaseCallCount > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, queue.ReleaseCallCount);
        Assert.Equal(0, queue.CompleteCallCount);
        Assert.Equal(0, executions.CancelCalls);
    }

    /// <summary>Resume(recovery) work item は RecoverExecution を呼ぶ。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenRecoveryResumeClaimed_CallsRecover()
    {
        // Arrange
        var executionId = Guid.Parse("70707070-7070-7070-7070-707070707070");
        var workItemId = Guid.Parse("80808080-8080-8080-8080-808080808080");
        var payload = JsonSerializer.Serialize(
            new ExecutionResumeWorkItemPayload(ExecutionResumeWorkItemModes.Recovery),
            ExecutionWorkItemPayloadJson.Options);
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Resume,
            Payload = payload,
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue { ItemsToClaim = [item] };
        var executions = new RecordingExecutionService { BeginGenerationToReturn = 1 };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new ExecutionWorkItemWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator());

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.CompleteCallCount > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, executions.RecoverCalls);
        Assert.Equal(1, queue.CompleteCallCount);
    }

    /// <summary>Cancel work item 成功時は CancelAsync と Complete が呼ばれる。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenCancelWorkItemClaimed_CompletesAfterCancel()
    {
        // Arrange
        var executionId = Guid.Parse("91919191-9191-9191-9191-919191919191");
        var workItemId = Guid.Parse("92929292-9292-9292-9292-929292929292");
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Cancel,
            Payload = "{}",
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue { ItemsToClaim = [item] };
        var executions = new RecordingExecutionService { BeginGenerationToReturn = 1 };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new ExecutionWorkItemWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator());

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.CompleteCallCount > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, executions.CancelCalls);
        Assert.Equal(1, queue.CompleteCallCount);
        Assert.Equal(0, executions.AwaitLocalLoadCalls);
    }

    /// <summary>Event Resume work item は ResumeNodeAsync を呼ぶ。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenEventResumeClaimed_CallsResumeNode()
    {
        // Arrange
        var executionId = Guid.Parse("93939393-9393-9393-9393-939393939393");
        var workItemId = Guid.Parse("94949494-9494-9494-9494-949494949494");
        var payload = JsonSerializer.Serialize(
            new ExecutionResumeWorkItemPayload(
                ExecutionResumeWorkItemModes.Event,
                NodeId: "wait-1",
                EventName: "Go",
                IdempotencyKey: "idem-1"),
            ExecutionWorkItemPayloadJson.Options);
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Resume,
            Payload = payload,
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue { ItemsToClaim = [item] };
        var executions = new RecordingExecutionService { BeginGenerationToReturn = 1 };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new ExecutionWorkItemWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator());

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.CompleteCallCount > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, executions.ResumeCalls);
        Assert.Equal(1, executions.AwaitLocalLoadCalls);
        Assert.Equal(1, queue.CompleteCallCount);
    }

    /// <summary>処理中例外は Release する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenProcessThrows_ReleasesWorkItem()
    {
        // Arrange
        var executionId = Guid.Parse("95959595-9595-9595-9595-959595959595");
        var workItemId = Guid.Parse("96969696-9696-9696-9696-969696969696");
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Cancel,
            Payload = "{}",
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue { ItemsToClaim = [item] };
        var executions = new RecordingExecutionService
        {
            BeginGenerationToReturn = 1,
            CancelException = new InvalidOperationException("cancel failed")
        };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new ExecutionWorkItemWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator());

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.ReleaseCallCount > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, queue.ReleaseCallCount);
        Assert.Equal(0, queue.CompleteCallCount);
    }

    /// <summary>非 Active テナントの work item は Complete してスキップする。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenTenantSuspended_CompletesWithoutSession()
    {
        // Arrange
        var executionId = Guid.Parse("97979797-9797-9797-9797-979797979797");
        var workItemId = Guid.Parse("98989898-9898-9898-9898-989898989898");
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Cancel,
            Payload = "{}",
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue { ItemsToClaim = [item] };
        var executions = new RecordingExecutionService();
        await using var provider = BuildProvider(
            queue,
            executions,
            new StubPlatformDataAccess
            {
                TenantToReturn = new ExecutionTenantLookup(
                    TestTenantIds.T1TenantId,
                    "t1",
                    TenantLifecycle.Suspended)
            });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new ExecutionWorkItemWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator());

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.CompleteCallCount > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, queue.CompleteCallCount);
        Assert.Equal(0, executions.BeginOwnedSessionCalls);
    }

    /// <summary>不正な Resume payload は Release する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenResumePayloadInvalid_ReleasesWorkItem()
    {
        // Arrange
        var executionId = Guid.Parse("99999999-9999-9999-9999-999999999991");
        var workItemId = Guid.Parse("99999999-9999-9999-9999-999999999992");
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Resume,
            Payload = "{}",
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue { ItemsToClaim = [item] };
        var executions = new RecordingExecutionService { BeginGenerationToReturn = 1 };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new ExecutionWorkItemWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator());

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.ReleaseCallCount > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, queue.ReleaseCallCount);
        Assert.Equal(0, queue.CompleteCallCount);
    }

    /// <summary>heartbeat で lease 更新失敗すると処理を中断する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenHeartbeatLeaseRenewFails_AbandonsSession()
    {
        // Arrange
        var executionId = Guid.Parse("99999999-9999-9999-9999-999999999993");
        var workItemId = Guid.Parse("99999999-9999-9999-9999-999999999994");
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Cancel,
            Payload = "{}",
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue { ItemsToClaim = [item], RenewLeaseResult = false };
        var executions = new RecordingExecutionService
        {
            BeginGenerationToReturn = 1,
            CancelDelay = TimeSpan.FromSeconds(25)
        };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        var sut = new ExecutionWorkItemWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator());

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => executions.AbandonCalls > 0, TimeSpan.FromSeconds(30));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(executions.AbandonCalls >= 1);
    }

    /// <summary>claim 反復例外でもポーリングを継続する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenClaimThrows_ContinuesAfterRetryDelay()
    {
        // Arrange
        var queue = new FakeWorkerQueue { ThrowOnClaim = true };
        var executions = new RecordingExecutionService();
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
        var sut = new ExecutionWorkItemWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator());

        // Act
        await sut.StartAsync(cts.Token);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }

        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(queue.ClaimCallCount >= 1);
    }

    /// <summary>セッション終了失敗時は Abandon にフォールバックする。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenEndOwnedSessionThrows_AbandonsSession()
    {
        // Arrange
        var executionId = Guid.Parse("99999999-9999-9999-9999-999999999995");
        var workItemId = Guid.Parse("99999999-9999-9999-9999-999999999996");
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Cancel,
            Payload = "{}",
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue { ItemsToClaim = [item] };
        var executions = new RecordingExecutionService
        {
            BeginGenerationToReturn = 1,
            EndOwnedSessionException = new InvalidOperationException("end failed")
        };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new ExecutionWorkItemWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator());

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => executions.AbandonCalls > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(executions.AbandonCalls >= 1);
        Assert.Equal(1, queue.CompleteCallCount);
    }

    /// <summary>heartbeat 例外でも処理 CTS をキャンセルする。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenHeartbeatThrows_AbandonsSession()
    {
        // Arrange
        var executionId = Guid.Parse("99999999-9999-9999-9999-999999999997");
        var workItemId = Guid.Parse("99999999-9999-9999-9999-999999999998");
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Cancel,
            Payload = "{}",
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue
        {
            ItemsToClaim = [item],
            RenewLeaseException = new InvalidOperationException("renew boom")
        };
        var executions = new RecordingExecutionService
        {
            BeginGenerationToReturn = 1,
            CancelDelay = TimeSpan.FromSeconds(25)
        };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        var sut = new ExecutionWorkItemWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator());

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => executions.AbandonCalls > 0, TimeSpan.FromSeconds(30));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(executions.AbandonCalls >= 1);
    }

    /// <summary>未知の work item kind は Release する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenKindUnknown_ReleasesWorkItem()
    {
        // Arrange
        var executionId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
        var workItemId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = "unknown-kind",
            Payload = "{}",
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue { ItemsToClaim = [item] };
        var executions = new RecordingExecutionService { BeginGenerationToReturn = 1 };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new ExecutionWorkItemWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator());

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.ReleaseCallCount > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, queue.ReleaseCallCount);
    }

    /// <summary>未知の resume mode は Release する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenResumeModeUnknown_ReleasesWorkItem()
    {
        // Arrange
        var executionId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");
        var workItemId = Guid.Parse("dddddddd-4444-4444-4444-444444444444");
        var payload = JsonSerializer.Serialize(
            new ExecutionResumeWorkItemPayload("weird-mode", null, null, null),
            ExecutionWorkItemPayloadJson.Options);
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Resume,
            Payload = payload,
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue { ItemsToClaim = [item] };
        var executions = new RecordingExecutionService { BeginGenerationToReturn = 1 };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new ExecutionWorkItemWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator());

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.ReleaseCallCount > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, queue.ReleaseCallCount);
    }

    /// <summary>event resume で nodeId/eventName 欠落は Release する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenEventResumeMissingFields_ReleasesWorkItem()
    {
        // Arrange
        var executionId = Guid.Parse("eeeeeeee-5555-5555-5555-555555555555");
        var workItemId = Guid.Parse("ffffffff-6666-6666-6666-666666666666");
        var payload = JsonSerializer.Serialize(
            new ExecutionResumeWorkItemPayload(ExecutionResumeWorkItemModes.Event, null, null, null),
            ExecutionWorkItemPayloadJson.Options);
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Resume,
            Payload = payload,
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue { ItemsToClaim = [item] };
        var executions = new RecordingExecutionService { BeginGenerationToReturn = 1 };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new ExecutionWorkItemWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator());

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.ReleaseCallCount > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, queue.ReleaseCallCount);
    }

    private static ServiceProvider BuildProvider(
        IExecutionWorkQueue queue,
        IExecutionService executions,
        IPlatformDataAccess platform)
    {
        var services = new ServiceCollection();
        services.AddSingleton(queue);
        services.AddSingleton(platform);
        services.AddSingleton<ITenantContextAccessor>(new SettableTenantContextAccessor());
        services.AddSingleton(executions);
        return services.BuildServiceProvider();
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (!predicate() && DateTime.UtcNow < deadline)
            await Task.Delay(20, CancellationToken.None);
    }

    private sealed class FixedIdGenerator : IIdGenerator
    {
        public Guid NewSequentialGuid() => Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public Guid NewRandomGuid() => Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    }

    private sealed class StubPlatformDataAccess : IPlatformDataAccess
    {
        public ExecutionTenantLookup? TenantToReturn { get; set; } = new(
            TestTenantIds.T1TenantId,
            "t1",
            TenantLifecycle.Active);

        public Task<ExecutionTenantLookup?> FindExecutionTenantAsync(
            Guid executionId,
            CancellationToken cancellationToken) =>
            Task.FromResult(TenantToReturn);

        public Task<TenantRow?> FindTenantByKeyAsync(string tenantKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TenantRow>> ListActiveTenantsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LoginCredentialLookup?> FindLoginCredentialAsync(
            string tenantKey,
            string username,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LoginCredentialLookup?> FindUserPrincipalAsync(
            Guid tenantId,
            Guid principalId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ApiKeyCredentialLookup?> FindApiKeyCredentialAsync(
            string keyPrefix,
            string keyHash,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task TouchApiKeyLastUsedAsync(Guid apiKeyId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task EnsureDefaultTenantAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task EnsurePermissionCatalogAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> ExpandPrincipalPermissionKeysAsync(
            Guid principalId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PrincipalRow?> FindPrincipalAsync(Guid principalId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<GroupSnapshot>> GetGroupSnapshotsForPrincipalAsync(
            Guid principalId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeWorkerQueue : IExecutionWorkQueue
    {
        private int _claimRound;

        public IReadOnlyList<ExecutionWorkItemRow> ItemsToClaim { get; set; } = [];
        public int ClaimCallCount { get; private set; }
        public int CompleteCallCount { get; private set; }
        public int ReleaseCallCount { get; private set; }
        public bool RenewLeaseResult { get; set; } = true;
        public bool ThrowOnClaim { get; set; }
        public Exception? RenewLeaseException { get; set; }

        public Task EnqueueAsync(ExecutionWorkItemRow item, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task EnqueueAsync(ICoreUnitOfWork uow, ExecutionWorkItemRow item, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task EnqueueManyAsync(IReadOnlyList<ExecutionWorkItemRow> items, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task EnqueueManyAsync(
            ICoreUnitOfWork uow,
            IReadOnlyList<ExecutionWorkItemRow> items,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<ExecutionWorkItemRow>> ClaimAsync(
            string leaseOwner,
            DateTime utcNow,
            TimeSpan leaseDuration,
            int limit,
            CancellationToken ct)
        {
            ClaimCallCount++;
            if (ThrowOnClaim)
                throw new InvalidOperationException("claim failed");
            if (_claimRound++ == 0 && ItemsToClaim.Count > 0)
                return Task.FromResult(ItemsToClaim);

            return Task.FromResult<IReadOnlyList<ExecutionWorkItemRow>>([]);
        }

        public Task CompleteAsync(Guid workItemId, string leaseOwner, CancellationToken ct)
        {
            CompleteCallCount++;
            return Task.CompletedTask;
        }

        public Task<bool> RenewLeaseAsync(
            Guid workItemId,
            string leaseOwner,
            DateTime utcNow,
            TimeSpan leaseDuration,
            CancellationToken ct)
        {
            if (RenewLeaseException is { } renewEx)
                throw renewEx;
            return Task.FromResult(RenewLeaseResult);
        }

        public Task ReleaseAsync(
            Guid workItemId,
            string leaseOwner,
            DateTime availableAt,
            CancellationToken ct)
        {
            ReleaseCallCount++;
            return Task.CompletedTask;
        }

        public Task<int> EnqueueExpiredOwnershipRecoveriesAsync(
            DateTime nowUtc,
            int limit,
            CancellationToken ct) =>
            Task.FromResult(0);

        public Task<int> EnqueueExpiredDelayWaitResumesAsync(
            DateTime nowUtc,
            int limit,
            CancellationToken ct) =>
            Task.FromResult(0);
    }

    private sealed class RecordingExecutionService : IExecutionService
    {
        public long? BeginGenerationToReturn { get; set; } = 1;
        public int BeginOwnedSessionCalls { get; private set; }
        public int EndOwnedSessionCalls { get; private set; }
        public int ExecuteQueuedStartCalls { get; private set; }
        public int AwaitLocalLoadCalls { get; private set; }
        public int CancelCalls { get; private set; }
        public int RecoverCalls { get; private set; }
        public int ResumeCalls { get; private set; }
        public int AbandonCalls { get; private set; }
        public Exception? CancelException { get; set; }
        public Exception? EndOwnedSessionException { get; set; }
        public TimeSpan? CancelDelay { get; set; }

        public Task<long?> BeginOwnedSessionAsync(
            Guid executionId,
            string workerId,
            TimeSpan leaseDuration,
            CancellationToken ct)
        {
            BeginOwnedSessionCalls++;
            return Task.FromResult(BeginGenerationToReturn);
        }

        public Task<bool> RenewOwnedSessionLeaseAsync(
            Guid executionId,
            TimeSpan leaseDuration,
            CancellationToken ct) =>
            Task.FromResult(true);

        public Task EndOwnedSessionAsync(Guid executionId, CancellationToken ct)
        {
            EndOwnedSessionCalls++;
            if (EndOwnedSessionException is { } endEx)
                throw endEx;
            return Task.CompletedTask;
        }

        public Task AbandonLocalOwnedSessionAsync(Guid executionId)
        {
            AbandonCalls++;
            return Task.CompletedTask;
        }

        public Task AwaitLocalExecutionLoadAsync(Guid executionId, CancellationToken ct)
        {
            AwaitLocalLoadCalls++;
            return Task.CompletedTask;
        }

        public Task<ExecutionResponse> ExecuteQueuedStartAsync(
            Guid executionId,
            StartExecutionRequest request,
            CancellationToken ct)
        {
            ExecuteQueuedStartCalls++;
            return Task.FromResult(new ExecutionResponse
            {
                DisplayId = "d",
                ResourceId = executionId,
                Status = ExecutionProjectionStatuses.Running,
                StartedAt = DateTime.UtcNow
            });
        }

        public async Task CancelAsync(
            string idOrUuid,
            string? idempotencyKey,
            CommandRequestContext requestContext,
            CancellationToken ct)
        {
            CancelCalls++;
            if (CancelException is { } cancelEx)
                throw cancelEx;
            if (CancelDelay is { } delay)
                await Task.Delay(delay, ct);
        }

        public Task RecoverExecutionAsync(
            Guid executionId,
            CommandRequestContext requestContext,
            CancellationToken ct)
        {
            RecoverCalls++;
            return Task.CompletedTask;
        }

        public Task ResumeNodeAsync(
            string idOrUuid,
            string nodeId,
            string? resumeKey,
            string? idempotencyKey,
            CommandRequestContext requestContext,
            CancellationToken ct)
        {
            ResumeCalls++;
            return Task.CompletedTask;
        }

        public Task<ExecutionResponse> StartAsync(
            StartExecutionRequest request,
            string? idempotencyKey,
            CommandRequestContext requestContext,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<PagedResult<ExecutionResponse>> ListPagedAsync(
            ExecutionListPageQuery query,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task PublishEventAsync(
            string idOrUuid,
            string eventName,
            string? idempotencyKey,
            CommandRequestContext requestContext,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task PersistCheckpointKeepLoadedAsync(string engineExecutionId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task PersistCheckpointAndUnloadAsync(Guid executionId, string nodeId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task PersistCheckpointAndUnloadByEngineIdAsync(
            string engineExecutionId,
            string nodeId,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(
            string idOrUuid,
            long atSeq,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ExecutionEventsResponseDto> ListEventsAsync(
            string idOrUuid,
            long afterSeq,
            int limit,
            CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
