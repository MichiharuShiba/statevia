using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Application.Contracts;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Application.Actions.Infrastructure;

/// <summary><see cref="IChildWorkflowRunner"/> の既定実装。</summary>
internal sealed class ChildWorkflowRunner : IChildWorkflowRunner
{
    private static readonly TimeSpan DefaultSyncTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SyncPollInterval = TimeSpan.FromMilliseconds(200);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantContextAccessor _tenantContext;

    /// <summary>スコープ付き実行サービス解決用に構築する。</summary>
    public ChildWorkflowRunner(IServiceScopeFactory scopeFactory, ITenantContextAccessor tenantContext)
    {
        _scopeFactory = scopeFactory;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc />
    public async Task<ChildWorkflowResult> RunAsync(ChildWorkflowRequest request, CancellationToken ct)
    {
        if (_tenantContext.TenantId is null)
        {
            throw new InvalidOperationException("Tenant context is required for workflow action.");
        }

        using var scope = _scopeFactory.CreateScope();
        var executionService = scope.ServiceProvider.GetRequiredService<IExecutionService>();

        var startRequest = new StartExecutionRequest
        {
            DefinitionId = request.DefinitionId,
            Input = ToJsonElement(request.Input),
        };

        var started = await executionService
            .StartAsync(
                startRequest,
                idempotencyKey: null,
                new CommandRequestContext("POST", "/internal/workflow-action"),
                ct)
            .ConfigureAwait(false);

        if (!string.Equals(request.Mode, "sync", StringComparison.OrdinalIgnoreCase))
        {
            return new ChildWorkflowResult(
                started.ResourceId.ToString("D"),
                started.DisplayId,
                started.Status);
        }

        var timeout = request.Timeout ?? DefaultSyncTimeout;
        var deadline = DateTime.UtcNow + timeout;
        var current = started;

        while (!IsTerminalStatus(current.Status) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(SyncPollInterval, ct).ConfigureAwait(false);
            current = await executionService
                .GetExecutionResponseAsync(current.ResourceId.ToString("D"), ct)
                .ConfigureAwait(false);
        }

        if (!IsTerminalStatus(current.Status))
        {
            throw new TimeoutException("workflow sync mode timed out.");
        }

        return new ChildWorkflowResult(
            current.ResourceId.ToString("D"),
            current.DisplayId,
            current.Status);
    }

    private static bool IsTerminalStatus(string status) =>
        status is "Completed" or "Failed" or "Cancelled";

    private static JsonElement? ToJsonElement(object? input)
    {
        if (input is null)
        {
            return null;
        }

        if (input is JsonElement jsonElement)
        {
            return jsonElement;
        }

        return JsonSerializer.SerializeToElement(input);
    }
}
