using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Core.Application.Contracts;
using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Core.Application.Contracts.Security;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Security;
using Statevia.Runtime.Configuration;
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
        var sut = CreateSut(provider);

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
        var sut = CreateSut(provider);

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
        var sut = CreateSut(provider);

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
        var sut = CreateSut(provider);

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.ReleaseCallCount > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, queue.ReleaseCallCount);
        Assert.Equal(0, queue.CompleteCallCount);
        Assert.Equal(0, executions.CancelCalls);
    }

    /// <summary>queued Start の Restore 不能は Failed 投影と Complete する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenQueuedStartRestoreFails_CompletesAndMarksFailed()
    {
        // Arrange
        var executionId = Guid.Parse("51515151-5151-5151-5151-515151515151");
        var workItemId = Guid.Parse("61616161-6161-6161-6161-616161616161");
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
            Attempts = 1,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue { ItemsToClaim = [item] };
        var executions = new RecordingExecutionService
        {
            BeginGenerationToReturn = 3,
            ExecuteQueuedStartException = new InvalidOperationException(
                WorkItemFailureClassifier.StoredDefinitionVersionInvalidMessage)
        };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = CreateSut(provider);

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.CompleteCallCount > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, queue.CompleteCallCount);
        Assert.Equal(0, queue.ReleaseCallCount);
        Assert.Equal(1, executions.MarkUnstartedPermanentFailureCalls);
    }

    /// <summary>未分類例外が試行上限に達した Resume は Failed と Complete する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenResumeReachesMaxAttempts_CompletesAndMarksFailed()
    {
        // Arrange
        var executionId = Guid.Parse("71717171-7171-7171-7171-717171717171");
        var workItemId = Guid.Parse("81818181-8181-8181-8181-818181818181");
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
            Attempts = 20,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue { ItemsToClaim = [item] };
        var executions = new RecordingExecutionService
        {
            BeginGenerationToReturn = 1,
            RecoverException = new InvalidOperationException("transient host error")
        };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = CreateSut(provider, new WorkerRuntimeOptions { MaxAttempts = 20 });

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.CompleteCallCount > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, queue.CompleteCallCount);
        Assert.Equal(0, queue.ReleaseCallCount);
        Assert.Equal(1, executions.MarkUnstartedPermanentFailureCalls);
    }

    /// <summary>所有獲得失敗は試行上限でも Release し、Failed にしない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenOwnershipMissAtMaxAttempts_ReleasesWithoutFailed()
    {
        // Arrange
        var executionId = Guid.Parse("12121212-1212-1212-1212-121212121212");
        var workItemId = Guid.Parse("13131313-1313-1313-1313-131313131313");
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Start,
            Payload = "{}",
            AvailableAt = DateTime.UtcNow,
            Attempts = 20,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue { ItemsToClaim = [item] };
        var executions = new RecordingExecutionService { BeginGenerationToReturn = null };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = CreateSut(provider, new WorkerRuntimeOptions { MaxAttempts = 20 });

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.ReleaseCallCount > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(queue.ReleaseWithoutCountingAttemptCallCount >= 1);
        Assert.True(queue.ReleaseCallCount >= 1);
        Assert.Equal(0, queue.CompleteCallCount);
        Assert.Equal(0, executions.MarkUnstartedPermanentFailureCalls);
    }

    /// <summary>所有 miss を MaxAttempts 回繰り返しても attempts は増えず Failed にしない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenOwnershipMissRepeatsToMaxAttempts_DoesNotIncreaseAttemptsOrFail()
    {
        // Arrange
        var executionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var workItemId = Guid.Parse("23232323-2323-2323-2323-232323232323");
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Start,
            Payload = "{}",
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        var queue = new FakeWorkerQueue
        {
            ItemsToClaim = [item],
            RequeueOnReleaseWithoutCountingAttempt = true
        };
        var executions = new RecordingExecutionService { BeginGenerationToReturn = null };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = CreateSut(provider, new WorkerRuntimeOptions { MaxAttempts = 3 });

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.ReleaseWithoutCountingAttemptCallCount >= 3, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(queue.ReleaseWithoutCountingAttemptCallCount >= 3);
        Assert.Equal(0, item.Attempts);
        Assert.Equal(0, queue.CompleteCallCount);
        Assert.Equal(0, executions.MarkUnstartedPermanentFailureCalls);
    }

    /// <summary>不正 Start を Complete したあと、別の正常 Start が claim できる。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenPoisonStartCompletes_NextStartIsClaimed()
    {
        // Arrange
        var poisonExecutionId = Guid.Parse("14141414-1414-1414-1414-141414141414");
        var okExecutionId = Guid.Parse("15151515-1515-1515-1515-151515151515");
        var poisonWorkItemId = Guid.Parse("16161616-1616-1616-1616-161616161616");
        var okWorkItemId = Guid.Parse("17171717-1717-1717-1717-171717171717");
        using var inputDoc = JsonDocument.Parse("{}");
        var poisonPayload = JsonSerializer.Serialize(
            new ExecutionStartWorkItemPayload(
                poisonExecutionId,
                new StartExecutionRequest { DefinitionId = "bad", Input = inputDoc.RootElement }),
            ExecutionWorkItemPayloadJson.Options);
        var okPayload = JsonSerializer.Serialize(
            new ExecutionStartWorkItemPayload(
                okExecutionId,
                new StartExecutionRequest { DefinitionId = "ok", Input = inputDoc.RootElement }),
            ExecutionWorkItemPayloadJson.Options);
        var queue = new FakeWorkerQueue
        {
            ItemsToClaim =
            [
                new ExecutionWorkItemRow
                {
                    WorkItemId = poisonWorkItemId,
                    ExecutionId = poisonExecutionId,
                    Kind = ExecutionWorkItemKinds.Start,
                    Payload = poisonPayload,
                    AvailableAt = DateTime.UtcNow,
                    Attempts = 1,
                    CreatedAt = DateTime.UtcNow
                },
                new ExecutionWorkItemRow
                {
                    WorkItemId = okWorkItemId,
                    ExecutionId = okExecutionId,
                    Kind = ExecutionWorkItemKinds.Start,
                    Payload = okPayload,
                    AvailableAt = DateTime.UtcNow,
                    Attempts = 1,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
        var executions = new RecordingExecutionService
        {
            BeginGenerationToReturn = 1,
            ExecuteQueuedStartFailOnce = true,
            ExecuteQueuedStartException = new InvalidOperationException(
                WorkItemFailureClassifier.StoredDefinitionVersionInvalidMessage)
        };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var sut = CreateSut(provider);

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.CompleteCallCount >= 2, TimeSpan.FromSeconds(2.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, queue.CompleteCallCount);
        Assert.Equal(0, queue.ReleaseCallCount);
        Assert.Equal(2, executions.ExecuteQueuedStartCalls);
        Assert.Equal(1, executions.MarkUnstartedPermanentFailureCalls);
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
        var sut = CreateSut(provider);

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
        var sut = CreateSut(provider);

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
        var sut = CreateSut(provider);

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
        var sut = CreateSut(provider);

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
        var sut = CreateSut(provider);

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
        var sut = CreateSut(provider);

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
        var sut = CreateSut(provider);

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
        var sut = CreateSut(provider);

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
        var sut = CreateSut(provider);

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
        var sut = CreateSut(provider);

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => executions.AbandonCalls > 0, TimeSpan.FromSeconds(30));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(executions.AbandonCalls >= 1);
    }

    /// <summary>未知の work item kind は Start/Resume/Cancel の claim 対象外のため処理されない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenKindUnknown_DoesNotClaimOrRelease()
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
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
        var sut = CreateSut(provider);

        // Act
        await sut.StartAsync(cts.Token);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }

        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, queue.ReleaseCallCount);
        Assert.Equal(0, queue.CompleteCallCount);
        Assert.Equal(0, executions.BeginOwnedSessionCalls);
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
        var sut = CreateSut(provider);

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
        var sut = CreateSut(provider);

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.ReleaseCallCount > 0, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, queue.ReleaseCallCount);
    }

    /// <summary>MaxConcurrency=2 のとき 2 件の Start が同時に AwaitLoad へ入る。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenMaxConcurrencyIsTwo_ProcessesTwoStartsInParallel()
    {
        // Arrange
        var firstId = Guid.Parse("11111111-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var secondId = Guid.Parse("22222222-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        using var inputDoc = JsonDocument.Parse("{}");
        var payload = JsonSerializer.Serialize(
            new ExecutionStartWorkItemPayload(
                firstId,
                new StartExecutionRequest { DefinitionId = "d1", Input = inputDoc.RootElement }),
            ExecutionWorkItemPayloadJson.Options);
        var queue = new FakeWorkerQueue
        {
            ItemsToClaim =
            [
                new ExecutionWorkItemRow
                {
                    WorkItemId = Guid.Parse("11111111-0000-0000-0000-000000000001"),
                    ExecutionId = firstId,
                    Kind = ExecutionWorkItemKinds.Start,
                    Payload = payload,
                    AvailableAt = DateTime.UtcNow,
                    Attempts = 0,
                    CreatedAt = DateTime.UtcNow
                },
                new ExecutionWorkItemRow
                {
                    WorkItemId = Guid.Parse("22222222-0000-0000-0000-000000000002"),
                    ExecutionId = secondId,
                    Kind = ExecutionWorkItemKinds.Start,
                    Payload = payload,
                    AvailableAt = DateTime.UtcNow,
                    Attempts = 0,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executions = new RecordingExecutionService
        {
            BeginGenerationToReturn = 1,
            AwaitLocalLoadGate = gate
        };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var sut = CreateSut(provider, new WorkerRuntimeOptions { MaxConcurrency = 2 });

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => executions.ConcurrentAwaitLocalLoadPeak >= 2, TimeSpan.FromSeconds(2));
        gate.TrySetResult();
        await WaitUntilAsync(() => queue.CompleteCallCount >= 2, TimeSpan.FromSeconds(1.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, executions.ExecuteQueuedStartCalls);
        Assert.True(executions.ConcurrentAwaitLocalLoadPeak >= 2);
        Assert.Equal(2, queue.CompleteCallCount);
        Assert.Equal(0, queue.ReleaseCallCount);
    }

    /// <summary>Start 所有中の Cancel は Engine CancelAsync を先に呼び、その後に Cancel item を Complete する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenCancelClaimedWhileStartOwned_CancelsProcessCtsWithoutSecondSession()
    {
        // Arrange
        var executionId = Guid.Parse("abababab-abab-abab-abab-abababababab");
        var startWorkItemId = Guid.Parse("cdcdcdcd-cdcd-cdcd-cdcd-cdcdcdcdcdcd");
        var cancelWorkItemId = Guid.Parse("efefefef-efef-efef-efef-efefefefefef");
        using var inputDoc = JsonDocument.Parse("{}");
        var payload = JsonSerializer.Serialize(
            new ExecutionStartWorkItemPayload(
                executionId,
                new StartExecutionRequest { DefinitionId = "d1", Input = inputDoc.RootElement }),
            ExecutionWorkItemPayloadJson.Options);
        var operationLog = new List<string>();
        var queue = new FakeWorkerQueue
        {
            OperationLog = operationLog,
            ItemsToClaim =
            [
                new ExecutionWorkItemRow
                {
                    WorkItemId = startWorkItemId,
                    ExecutionId = executionId,
                    Kind = ExecutionWorkItemKinds.Start,
                    Payload = payload,
                    AvailableAt = DateTime.UtcNow,
                    Attempts = 0,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executions = new RecordingExecutionService
        {
            OperationLog = operationLog,
            BeginGenerationToReturn = 1,
            AwaitLocalLoadGate = gate
        };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        var sut = CreateSut(provider);

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => executions.AwaitLocalLoadCalls >= 1, TimeSpan.FromSeconds(2));
        queue.ItemsToClaim.Add(new ExecutionWorkItemRow
        {
            WorkItemId = cancelWorkItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Cancel,
            Payload = "{}",
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        });
        await WaitUntilAsync(() => queue.CompleteCallCount >= 2, TimeSpan.FromSeconds(2.5));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, executions.BeginOwnedSessionCalls);
        Assert.True(executions.CancelCalls >= 1);
        Assert.Equal(2, queue.CompleteCallCount);
        Assert.Equal(0, queue.ReleaseCallCount);
        var cancelIndex = operationLog.IndexOf("cancel");
        var cancelCompleteIndex = operationLog.IndexOf($"complete:{cancelWorkItemId:D}");
        Assert.True(cancelIndex >= 0);
        Assert.True(cancelCompleteIndex >= 0);
        Assert.True(cancelIndex < cancelCompleteIndex);
    }

    /// <summary>AwaitLoad が無進捗相当で戻っても Complete し Release しない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenAwaitLoadReturnsAfterNoProgress_CompletesWithoutRelease()
    {
        // Arrange
        var executionId = Guid.Parse("12121212-1212-1212-1212-121212121212");
        var workItemId = Guid.Parse("34343434-3434-3434-3434-343434343434");
        using var inputDoc = JsonDocument.Parse("{}");
        var payload = JsonSerializer.Serialize(
            new ExecutionStartWorkItemPayload(
                executionId,
                new StartExecutionRequest { DefinitionId = "d1", Input = inputDoc.RootElement }),
            ExecutionWorkItemPayloadJson.Options);
        var queue = new FakeWorkerQueue
        {
            ItemsToClaim =
            [
                new ExecutionWorkItemRow
                {
                    WorkItemId = workItemId,
                    ExecutionId = executionId,
                    Kind = ExecutionWorkItemKinds.Start,
                    Payload = payload,
                    AvailableAt = DateTime.UtcNow,
                    Attempts = 0,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
        var executions = new RecordingExecutionService
        {
            BeginGenerationToReturn = 1,
            AwaitLocalLoadDelay = TimeSpan.FromMilliseconds(80)
        };
        await using var provider = BuildProvider(queue, executions, new StubPlatformDataAccess());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var sut = CreateSut(provider);

        // Act
        await sut.StartAsync(cts.Token);
        await WaitUntilAsync(() => queue.CompleteCallCount > 0, TimeSpan.FromSeconds(2));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, queue.CompleteCallCount);
        Assert.Equal(0, queue.ReleaseCallCount);
        Assert.Equal(0, executions.CancelCalls);
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

    private static ExecutionWorkItemWorkerHostedService CreateSut(
        IServiceProvider provider,
        WorkerRuntimeOptions? worker = null) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionWorkItemWorkerHostedService>.Instance,
            new FixedIdGenerator(),
            Options.Create(worker ?? new WorkerRuntimeOptions()));

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

        public Task<bool> TryUpdateUserPasswordHashAsync(
            Guid tenantId,
            Guid userId,
            string passwordHash,
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
        private readonly HashSet<Guid> _claimed = [];

        public List<ExecutionWorkItemRow> ItemsToClaim { get; set; } = [];
        public List<string>? OperationLog { get; set; }
        public int ClaimCallCount { get; private set; }
        public int CompleteCallCount { get; private set; }
        public int ReleaseCallCount { get; private set; }
        public int ReleaseWithoutCountingAttemptCallCount { get; private set; }
        public bool RequeueOnReleaseWithoutCountingAttempt { get; set; }
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
            IReadOnlyList<string>? kinds,
            CancellationToken ct)
        {
            _ = leaseOwner;
            _ = utcNow;
            _ = leaseDuration;
            ClaimCallCount++;
            if (ThrowOnClaim)
                throw new InvalidOperationException("claim failed");

            var pending = ItemsToClaim.Where(item => !_claimed.Contains(item.WorkItemId));
            if (kinds is { Count: > 0 })
            {
                pending = pending.Where(item =>
                    kinds.Contains(item.Kind, StringComparer.Ordinal));
            }

            var batch = pending.Take(limit).ToList();
            foreach (var item in batch)
            {
                item.Attempts += 1;
                _claimed.Add(item.WorkItemId);
            }

            return Task.FromResult<IReadOnlyList<ExecutionWorkItemRow>>(batch);
        }

        public Task CompleteAsync(Guid workItemId, string leaseOwner, CancellationToken ct)
        {
            CompleteCallCount++;
            OperationLog?.Add($"complete:{workItemId:D}");
            return Task.CompletedTask;
        }

        public Task CompleteIncompleteStartItemsAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
            Task.CompletedTask;

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

        public Task ReleaseWithoutCountingAttemptAsync(
            Guid workItemId,
            string leaseOwner,
            DateTime availableAt,
            CancellationToken ct)
        {
            ReleaseWithoutCountingAttemptCallCount++;
            ReleaseCallCount++;
            var item = ItemsToClaim.Find(row => row.WorkItemId == workItemId);
            if (item is not null && item.Attempts > 0)
                item.Attempts -= 1;
            if (RequeueOnReleaseWithoutCountingAttempt)
                _claimed.Remove(workItemId);
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
        private int _awaitLocalInFlight;

        public long? BeginGenerationToReturn { get; set; } = 1;
        public int BeginOwnedSessionCalls { get; private set; }
        public int EndOwnedSessionCalls { get; private set; }
        public int ExecuteQueuedStartCalls { get; private set; }
        public int AwaitLocalLoadCalls { get; private set; }
        public int ConcurrentAwaitLocalLoad { get; private set; }
        public int ConcurrentAwaitLocalLoadPeak { get; private set; }
        public int CancelCalls { get; private set; }
        public List<string>? OperationLog { get; set; }
        public int RecoverCalls { get; private set; }
        public int ResumeCalls { get; private set; }
        public int AbandonCalls { get; private set; }
        public int MarkUnstartedPermanentFailureCalls { get; private set; }
        public Exception? CancelException { get; set; }
        public Exception? ExecuteQueuedStartException { get; set; }
        public bool ExecuteQueuedStartFailOnce { get; set; }
        public Exception? RecoverException { get; set; }
        public Exception? EndOwnedSessionException { get; set; }
        public TimeSpan? CancelDelay { get; set; }
        public TimeSpan? AwaitLocalLoadDelay { get; set; }
        public TaskCompletionSource? AwaitLocalLoadGate { get; set; }

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

        public async Task AwaitLocalExecutionLoadAsync(
            Guid executionId,
            TimeSpan noProgressTimeout,
            CancellationToken ct)
        {
            _ = executionId;
            _ = noProgressTimeout;
            var inFlight = Interlocked.Increment(ref _awaitLocalInFlight);
            if (inFlight > ConcurrentAwaitLocalLoadPeak)
                ConcurrentAwaitLocalLoadPeak = inFlight;
            ConcurrentAwaitLocalLoad = inFlight;
            AwaitLocalLoadCalls++;
            try
            {
                if (AwaitLocalLoadGate is { } gate)
                    await gate.Task.WaitAsync(ct);
                if (AwaitLocalLoadDelay is { } delay)
                    await Task.Delay(delay, ct);
            }
            finally
            {
                ConcurrentAwaitLocalLoad = Interlocked.Decrement(ref _awaitLocalInFlight);
            }
        }

        public Task<ExecutionResponse> ExecuteQueuedStartAsync(
            Guid executionId,
            StartExecutionRequest request,
            CancellationToken ct)
        {
            ExecuteQueuedStartCalls++;
            if (ExecuteQueuedStartException is { } startEx
                && (!ExecuteQueuedStartFailOnce || ExecuteQueuedStartCalls == 1))
            {
                throw startEx;
            }
            return Task.FromResult(new ExecutionResponse
            {
                DisplayId = "d",
                ResourceId = executionId,
                Status = ExecutionProjectionStatuses.Running,
                StartedAt = DateTime.UtcNow
            });
        }

        public Task MarkUnstartedPermanentFailureAsync(Guid executionId, CancellationToken ct)
        {
            _ = executionId;
            _ = ct;
            MarkUnstartedPermanentFailureCalls++;
            return Task.CompletedTask;
        }

        public async Task CancelAsync(
            string idOrUuid,
            string? idempotencyKey,
            CommandRequestContext requestContext,
            CancellationToken ct)
        {
            CancelCalls++;
            OperationLog?.Add("cancel");
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
            if (RecoverException is { } recoverEx)
                throw recoverEx;
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

        public Task<ExecutionWaitsResponse> GetExecutionWaitsAsync(string idOrUuid, CancellationToken ct) =>
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
