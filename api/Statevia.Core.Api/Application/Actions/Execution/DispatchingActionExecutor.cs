using Statevia.Actions.Abstractions.Catalog;
using Statevia.Actions.Abstractions.Execution;
using Statevia.Actions.Abstractions.Visibility;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Api.Application.Actions.Execution;

/// <summary>Catalog → Visibility → Policy → Backend の実行ディスパッチャ。</summary>
internal sealed class DispatchingActionExecutor : IActionExecutor
{
    private readonly IActionCatalog _catalog;
    private readonly IActionVisibilityResolver _visibilityResolver;
    private readonly IActionExecutionPolicy _executionPolicy;
    private readonly InProcessBackend _inProcessBackend;
    private readonly IHostEnvironment _hostEnvironment;

    /// <summary>
    /// ディスパッチャを構築する。
    /// </summary>
    public DispatchingActionExecutor(
        IActionCatalog catalog,
        IActionVisibilityResolver visibilityResolver,
        IActionExecutionPolicy executionPolicy,
        InProcessBackend inProcessBackend,
        IHostEnvironment hostEnvironment)
    {
        _catalog = catalog;
        _visibilityResolver = visibilityResolver;
        _executionPolicy = executionPolicy;
        _inProcessBackend = inProcessBackend;
        _hostEnvironment = hostEnvironment;
    }

    /// <inheritdoc />
    public async Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        StateContext stateContext,
        object? runtimeInput,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(stateContext);

        if (!_catalog.TryGetRegistration(request.ActionId, out var registration) || registration is null)
        {
            return Failure("UnknownAction", $"Unknown action '{request.ActionId}'.");
        }

        if (!_visibilityResolver.CanUse(request.TenantId, registration.Descriptor))
        {
            return Failure("ActionNotVisible", $"Action '{request.ActionId}' is not visible to the current tenant.");
        }

        var mode = _executionPolicy.Resolve(
            new ActionExecutionContext(
                request.TenantId,
                _hostEnvironment.EnvironmentName,
                DeploymentProfile: null),
            registration.Descriptor);

        if (mode != ActionExecutionMode.InProcess)
        {
            throw new NotSupportedException(
                $"Action execution mode '{mode}' is not supported in Phase 1.");
        }

        var output = await _inProcessBackend
            .ExecuteAsync(registration, stateContext, runtimeInput, cancellationToken)
            .ConfigureAwait(false);

        return new ActionExecutionResult
        {
            Success = true,
            RuntimeOutput = output,
        };
    }

    private static ActionExecutionResult Failure(string errorCode, string message) =>
        new()
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = message,
        };
}
