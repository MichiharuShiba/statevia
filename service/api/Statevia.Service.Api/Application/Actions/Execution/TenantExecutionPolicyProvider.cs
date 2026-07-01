using Microsoft.Extensions.Options;
using Statevia.Core.Actions.Abstractions.Execution;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>appsettings の Tenant スコープ設定から実行ポリシーを供給する provider。</summary>
/// <remarks>本フェーズは Tenant scope のみ実装する。Org / Project / Environment は将来の別 provider で追加する。</remarks>
/// <param name="options">Tenant スコープ設定を含む実行ポリシー設定。</param>
internal sealed class TenantExecutionPolicyProvider(IOptions<ExecutionPolicyOptions> options) : IExecutionPolicyProvider
{
    private readonly ExecutionPolicyOptions _options = options.Value;

    /// <inheritdoc />
    public IReadOnlyList<ScopedExecutionPolicy> GetPolicies(ActionExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_options.Tenants.TryGetValue(context.TenantId, out var tenantPolicy)
            && tenantPolicy.MinimumMode is { } minimumMode)
        {
            return [new ScopedExecutionPolicy(ExecutionPolicyScope.Tenant, new ExecutionPolicy(minimumMode))];
        }

        return [];
    }
}
