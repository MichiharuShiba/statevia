using Microsoft.Extensions.Options;
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
    private readonly IActionExecutionBackendSelector _backendSelector;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ExecutionPolicyOptions _policyOptions;

    /// <summary>
    /// ディスパッチャを構築する。
    /// </summary>
    public DispatchingActionExecutor(
        IActionCatalog catalog,
        IActionVisibilityResolver visibilityResolver,
        IActionExecutionPolicy executionPolicy,
        IActionExecutionBackendSelector backendSelector,
        IHostEnvironment hostEnvironment,
        IOptions<ExecutionPolicyOptions> policyOptions)
    {
        _catalog = catalog;
        _visibilityResolver = visibilityResolver;
        _executionPolicy = executionPolicy;
        _backendSelector = backendSelector;
        _hostEnvironment = hostEnvironment;
        _policyOptions = policyOptions.Value;
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

        var context = new ActionExecutionContext(
            request.TenantId,
            _hostEnvironment.EnvironmentName,
            _policyOptions.DeploymentProfile);

        var mode = _executionPolicy.Resolve(context, registration.Descriptor);

        if (!_backendSelector.TryResolve(mode, context, out var backend))
        {
            return Failure(
                "UnsupportedExecutionMode",
                $"No execution backend is registered for mode '{mode}'.");
        }

        var invocation = new ActionBackendInvocation(
            request,
            runtimeInput,
            registration,
            stateContext);

        return await backend.ExecuteAsync(invocation, cancellationToken).ConfigureAwait(false);
    }

    private static ActionExecutionResult Failure(string errorCode, string message) =>
        new()
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = message,
        };
}
