using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Actions.Abstractions.Visibility;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Application.Actions.Execution;

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

        if (!TryGetRegistration(request, out var registration) || registration is null)
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

    private bool TryGetRegistration(
        ActionExecutionRequest request,
        [NotNullWhen(true)] out ActionRegistration? registration)
    {
        registration = null;
        if (request.ResolvedModuleVersion is { Length: > 0 } version
            && TryParseVersionedAction(request.ActionId, out var moduleId, out var actionName))
        {
            return _catalog.TryGetRegistration(moduleId, version, actionName, out registration);
        }

        return _catalog.TryGetRegistration(request.ActionId, out registration);
    }

    private static bool TryParseVersionedAction(
        string logicalActionId,
        out string moduleId,
        out string actionName)
    {
        moduleId = string.Empty;
        actionName = string.Empty;

        var lastDot = logicalActionId.LastIndexOf('.');
        if (lastDot <= 0 || lastDot >= logicalActionId.Length - 1)
        {
            return false;
        }

        moduleId = logicalActionId[..lastDot];
        actionName = logicalActionId[(lastDot + 1)..];
        return true;
    }
}
