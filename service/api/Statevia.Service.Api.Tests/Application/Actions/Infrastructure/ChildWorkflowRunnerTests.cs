using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Application.Contracts;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Service.Api.Application.Actions.Infrastructure;
using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Tests.Application.Actions.Infrastructure;

/// <summary><see cref="ChildWorkflowRunner"/> の単体テスト。</summary>
public sealed class ChildWorkflowRunnerTests
{
    /// <summary>async モードは起動結果を即返却する。</summary>
    [Fact]
    public async Task RunAsync_AsyncMode_ReturnsStartedExecution()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionService>(new FakeExecutionService
        {
            StartResult = new ExecutionResponse
            {
                DisplayId = "wf-1",
                ResourceId = executionId,
                Status = "Running",
            },
        });
        services.AddSingleton<ITenantContextAccessor>(new FakeTenantContextAccessor(Guid.NewGuid()));
        var provider = services.BuildServiceProvider();
        var runner = new ChildWorkflowRunner(provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<ITenantContextAccessor>());

        // Act
        var result = await runner.RunAsync(
            new ChildWorkflowRequest("def-1", "async", Input: null, Timeout: null),
            CancellationToken.None);

        // Assert
        Assert.Equal(executionId.ToString("D"), result.WorkflowId);
        Assert.Equal("wf-1", result.DisplayId);
        Assert.Equal("Running", result.Status);
    }

    /// <summary>テナント文脈が無い場合は例外になる。</summary>
    [Fact]
    public async Task RunAsync_WithoutTenant_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionService>(new FakeExecutionService());
        services.AddSingleton<ITenantContextAccessor>(new FakeTenantContextAccessor(tenantId: null));
        var provider = services.BuildServiceProvider();
        var runner = new ChildWorkflowRunner(provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<ITenantContextAccessor>());

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync(new ChildWorkflowRequest("def-1", "async", null, null), CancellationToken.None));
    }

    /// <summary>sync モードは終端 status までポーリングする。</summary>
    [Fact]
    public async Task RunAsync_SyncMode_PollsUntilTerminal()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var services = new ServiceCollection();
        var executions = new FakeExecutionService
        {
            StartResult = new ExecutionResponse
            {
                DisplayId = "wf-sync",
                ResourceId = executionId,
                Status = "Running",
            },
            PollResults =
            [
                new ExecutionResponse { DisplayId = "wf-sync", ResourceId = executionId, Status = "Running" },
                new ExecutionResponse { DisplayId = "wf-sync", ResourceId = executionId, Status = "Completed" },
            ]
        };
        services.AddSingleton<IExecutionService>(executions);
        services.AddSingleton<ITenantContextAccessor>(new FakeTenantContextAccessor(Guid.NewGuid()));
        await using var provider = services.BuildServiceProvider();
        var runner = new ChildWorkflowRunner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ITenantContextAccessor>());

        // Act
        var result = await runner.RunAsync(
            new ChildWorkflowRequest("def-1", "sync", Input: new { x = 1 }, Timeout: TimeSpan.FromSeconds(5)),
            CancellationToken.None);

        // Assert
        Assert.Equal("Completed", result.Status);
        Assert.True(executions.GetCallCount >= 1);
    }

    /// <summary>sync が終端に達しないと TimeoutException。</summary>
    [Fact]
    public async Task RunAsync_SyncMode_WhenNeverTerminal_ThrowsTimeout()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionService>(new FakeExecutionService
        {
            StartResult = new ExecutionResponse
            {
                DisplayId = "wf-to",
                ResourceId = executionId,
                Status = "Running",
            }
        });
        services.AddSingleton<ITenantContextAccessor>(new FakeTenantContextAccessor(Guid.NewGuid()));
        await using var provider = services.BuildServiceProvider();
        var runner = new ChildWorkflowRunner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ITenantContextAccessor>());

        // Act + Assert
        await Assert.ThrowsAsync<TimeoutException>(() =>
            runner.RunAsync(
                new ChildWorkflowRequest("def-1", "sync", null, TimeSpan.FromMilliseconds(50)),
                CancellationToken.None));
    }

    /// <summary>JsonElement 入力はそのまま Start に渡る。</summary>
    [Fact]
    public async Task RunAsync_WithJsonElementInput_StartsSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var executions = new FakeExecutionService
        {
            StartResult = new ExecutionResponse
            {
                DisplayId = "wf-json",
                ResourceId = Guid.NewGuid(),
                Status = "Running",
            }
        };
        services.AddSingleton<IExecutionService>(executions);
        services.AddSingleton<ITenantContextAccessor>(new FakeTenantContextAccessor(Guid.NewGuid()));
        await using var provider = services.BuildServiceProvider();
        var runner = new ChildWorkflowRunner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ITenantContextAccessor>());
        using var doc = JsonDocument.Parse("""{"k":"v"}""");

        // Act
        var result = await runner.RunAsync(
            new ChildWorkflowRequest("def-1", "async", doc.RootElement, null),
            CancellationToken.None);

        // Assert
        Assert.Equal("Running", result.Status);
    }

    private sealed class FakeTenantContextAccessor : ITenantContextAccessor
    {
        private readonly Guid? _tenantId;

        public FakeTenantContextAccessor(Guid? tenantId) => _tenantId = tenantId;

        public bool IsResolved => _tenantId is not null;

        public Guid? TenantId => _tenantId;

        public string? TenantKey => null;

        public Guid? PrincipalId => null;

        public IReadOnlySet<string>? EffectivePermissionKeys => null;

        public IDisposable SetContext(TenantContextState? state) => new NoopDisposable();
    }

    private sealed class FakeExecutionService : IExecutionService
    {
        private int _pollIndex;

        public ExecutionResponse StartResult { get; init; } = new()
        {
            DisplayId = "wf",
            ResourceId = Guid.NewGuid(),
            Status = "Running",
        };

        public IReadOnlyList<ExecutionResponse> PollResults { get; init; } = [];

        public int GetCallCount { get; private set; }

        public Task<ExecutionResponse> StartAsync(
            StartExecutionRequest request,
            string? idempotencyKey,
            CommandRequestContext requestContext,
            CancellationToken ct) =>
            Task.FromResult(StartResult);

        public Task<PagedResult<ExecutionResponse>> ListPagedAsync(ExecutionListPageQuery query, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct)
        {
            GetCallCount += 1;
            if (_pollIndex < PollResults.Count)
            {
                var next = PollResults[_pollIndex];
                _pollIndex += 1;
                return Task.FromResult(next);
            }

            return Task.FromResult(StartResult);
        }

        public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ExecutionWaitsResponse> GetExecutionWaitsAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ExecutionEventsResponseDto> ListEventsAsync(string idOrUuid, long afterSeq, int limit, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task ResumeNodeAsync(
            string idOrUuid,
            string nodeId,
            string? resumeKey,
            string? idempotencyKey,
            CommandRequestContext requestContext,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task CancelAsync(
            string idOrUuid,
            string? idempotencyKey,
            CommandRequestContext requestContext,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task PublishEventAsync(
            string idOrUuid,
            string eventName,
            string? idempotencyKey,
            CommandRequestContext requestContext,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct) =>
            Task.CompletedTask;

        public Task PersistCheckpointAndUnloadAsync(Guid executionId, string nodeId, CancellationToken ct) => Task.CompletedTask;
        public Task RecoverExecutionAsync(Guid executionId, CommandRequestContext requestContext, CancellationToken ct) => Task.CompletedTask;
        public Task<ExecutionResponse> ExecuteQueuedStartAsync(Guid executionId, StartExecutionRequest request, CancellationToken ct) => Task.FromResult(new ExecutionResponse());
        public Task PersistCheckpointKeepLoadedAsync(string engineExecutionId, CancellationToken ct) => Task.CompletedTask;
        public Task<long?> BeginOwnedSessionAsync(Guid executionId, string workerId, TimeSpan leaseDuration, CancellationToken ct) => Task.FromResult<long?>(1);
        public Task<bool> RenewOwnedSessionLeaseAsync(Guid executionId, TimeSpan leaseDuration, CancellationToken ct) => Task.FromResult(true);
        public Task EndOwnedSessionAsync(Guid executionId, CancellationToken ct) => Task.CompletedTask;
        public Task AbandonLocalOwnedSessionAsync(Guid executionId) => Task.CompletedTask;

        public Task AwaitLocalExecutionLoadAsync(Guid executionId, CancellationToken ct) => Task.CompletedTask;
        public Task PersistCheckpointAndUnloadByEngineIdAsync(string engineExecutionId, string nodeId, CancellationToken ct) => Task.CompletedTask;

    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
