using Microsoft.Extensions.Options;
using Statevia.Actions.Abstractions.Catalog;
using Statevia.Actions.Abstractions.Execution;

namespace Statevia.Core.Api.Application.Actions.Execution;

/// <summary>
/// TrustLevel × Environment × DeploymentProfile と ExecutionHints から最終実行モードを決定する。
/// TrustLevel 下限は緩和できない。
/// </summary>
internal sealed class ConfigurableExecutionPolicy : IActionExecutionPolicy
{
    private readonly ExecutionPolicyOptions _options;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="options">Policy 設定。</param>
    public ConfigurableExecutionPolicy(IOptions<ExecutionPolicyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public ActionExecutionMode Resolve(ActionExecutionContext context, ActionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(descriptor);

        var deploymentProfile = context.DeploymentProfile ?? _options.DeploymentProfile;
        var trustMinimum = ResolveTrustEnvironmentMode(
            descriptor.TrustLevel,
            context.Environment,
            deploymentProfile);

        var mode = trustMinimum;

        if (descriptor.ExecutionHints.RequiresIsolation)
        {
            mode = ActionExecutionModeStrictness.Max(mode, ActionExecutionMode.OutOfProcess);
        }

        if (descriptor.ExecutionHints.PreferredMode is { } preferredMode)
        {
            mode = ActionExecutionModeStrictness.Max(mode, preferredMode);
        }

        if (descriptor.ExecutionHints.AllowedModes is { Count: > 0 } allowedModes)
        {
            mode = ConstrainToAllowedModes(mode, allowedModes, trustMinimum);
        }

        return mode;
    }

    private static ActionExecutionMode ResolveTrustEnvironmentMode(
        ActionTrustLevel trustLevel,
        string environment,
        string? deploymentProfile)
    {
        var isDevelopment = string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
        var isSaasShared = IsSaasSharedProfile(deploymentProfile);

        return (trustLevel, isDevelopment, isSaasShared) switch
        {
            (ActionTrustLevel.Trusted, _, _) => ActionExecutionMode.InProcess,
            (ActionTrustLevel.Verified, true, _) => ActionExecutionMode.InProcess,
            (ActionTrustLevel.Verified, false, _) => ActionExecutionMode.OutOfProcess,
            // Signed（改ざんなしのみ保証・署名者未信頼）は緩和対象外。Community 相当に固定する。
            (ActionTrustLevel.Signed, _, _) => ActionExecutionMode.OutOfProcess,
            (ActionTrustLevel.Community, _, _) => ActionExecutionMode.OutOfProcess,
            (ActionTrustLevel.Untrusted, _, true) => ActionExecutionMode.Container,
            (ActionTrustLevel.Untrusted, _, false) => ActionExecutionMode.OutOfProcess,
            _ => ActionExecutionMode.OutOfProcess,
        };
    }

    private static bool IsSaasSharedProfile(string? deploymentProfile) =>
        string.Equals(deploymentProfile, "saas-shared", StringComparison.OrdinalIgnoreCase)
        || string.Equals(deploymentProfile, "SaaSShared", StringComparison.OrdinalIgnoreCase);

    private static ActionExecutionMode ConstrainToAllowedModes(
        ActionExecutionMode mode,
        IReadOnlySet<ActionExecutionMode> allowedModes,
        ActionExecutionMode trustMinimum)
    {
        if (allowedModes.Contains(mode))
        {
            return mode;
        }

        var eligible = allowedModes
            .Where(candidate => ActionExecutionModeStrictness.Rank(candidate) >= ActionExecutionModeStrictness.Rank(trustMinimum))
            .OrderBy(ActionExecutionModeStrictness.Rank)
            .ToList();

        if (eligible.Count == 0)
        {
            return ActionExecutionModeStrictness.Max(mode, trustMinimum);
        }

        var atOrAboveMode = eligible
            .Where(candidate => ActionExecutionModeStrictness.Rank(candidate) >= ActionExecutionModeStrictness.Rank(mode))
            .ToList();

        return atOrAboveMode.Count > 0
            ? atOrAboveMode[0]
            : eligible[^1];
    }
}
