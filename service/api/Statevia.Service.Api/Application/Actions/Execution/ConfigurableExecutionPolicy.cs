using Microsoft.Extensions.Options;
using Statevia.Actions.Abstractions.Catalog;
using Statevia.Actions.Abstractions.Execution;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>
/// TrustLevel × Environment × DeploymentProfile（base）と階層 Policy、ExecutionHints から最終実行モードを決定する。
/// base 下限・各階層の下限はいずれも緩和できない（最厳優先）。
/// </summary>
/// <param name="options">Policy 設定。</param>
/// <param name="policyProviders">階層別ポリシー provider 群（base へ最厳優先で重ねる）。</param>
internal sealed class ConfigurableExecutionPolicy(
    IOptions<ExecutionPolicyOptions> options,
    IEnumerable<IExecutionPolicyProvider> policyProviders) : IActionExecutionPolicy
{
    private readonly ExecutionPolicyOptions _options = options.Value;
    private readonly IReadOnlyList<IExecutionPolicyProvider> _policyProviders = [.. policyProviders];

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

        // base 下限へ階層ポリシーを最厳優先で重ねたものが、緩和不可の実効下限になる。
        var floor = ApplyScopedPolicies(trustMinimum, context);

        var mode = floor;

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
            mode = ConstrainToAllowedModes(mode, allowedModes, floor);
        }

        return mode;
    }

    /// <summary>base 下限へ全 provider の階層ポリシー下限を最厳優先で重ねる。</summary>
    /// <param name="trustMinimum">TrustLevel × Environment による base 下限。</param>
    /// <param name="context">実行コンテキスト。</param>
    /// <returns>緩和不可の実効下限。</returns>
    private ActionExecutionMode ApplyScopedPolicies(
        ActionExecutionMode trustMinimum,
        ActionExecutionContext context)
    {
        var scopedMinimums = _policyProviders
            .SelectMany(provider => provider.GetPolicies(context))
            .Select(scoped => scoped.Policy.MinimumMode)
            .Where(minimumMode => minimumMode is not null)
            .Select(minimumMode => minimumMode!.Value);

        return scopedMinimums.Aggregate(trustMinimum, ActionExecutionModeStrictness.Max);
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
