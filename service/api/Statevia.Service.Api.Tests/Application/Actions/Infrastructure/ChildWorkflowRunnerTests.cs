using Microsoft.Extensions.DependencyInjection;
using Statevia.Service.Api.Abstractions.Security;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Application.Actions.Infrastructure;
using Statevia.Service.Api.Controllers;
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
        public ExecutionResponse StartResult { get; init; } = new()
        {
            DisplayId = "wf",
            ResourceId = Guid.NewGuid(),
            Status = "Running",
        };

        public Task<ExecutionResponse> StartAsync(
            StartExecutionRequest request,
            string? idempotencyKey,
            CommandRequestContext requestContext,
            CancellationToken ct) =>
            Task.FromResult(StartResult);

        public Task<PagedResult<ExecutionResponse>> ListPagedAsync(ExecutionListQuery query, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct) =>
            Task.FromResult(StartResult);

        public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct) =>
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
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
