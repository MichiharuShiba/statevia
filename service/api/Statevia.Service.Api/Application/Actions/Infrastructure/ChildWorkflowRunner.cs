using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Application.Contracts;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Application.Actions.Infrastructure;

/// <summary><see cref="IChildWorkflowRunner"/> の既定実装。</summary>
/// <remarks>
/// 子開始は常に非同期（開始直後の status を返す）。子の終端待ちは Wait / Resume で行う。
/// </remarks>
internal sealed class ChildWorkflowRunner : IChildWorkflowRunner
{
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

        if (request.ParentExecutionId == Guid.Empty)
        {
            throw new ArgumentException("workflow action requires a parent execution id.");
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
                new CommandRequestContext("POST", "/internal/workflow-action", request.ParentExecutionId),
                ct)
            .ConfigureAwait(false);

        return new ChildWorkflowResult(
            started.ResourceId.ToString("D"),
            started.DisplayId,
            started.Status);
    }

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
