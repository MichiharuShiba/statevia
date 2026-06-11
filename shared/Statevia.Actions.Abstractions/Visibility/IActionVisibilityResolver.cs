using Statevia.Actions.Abstractions.Catalog;

namespace Statevia.Actions.Abstractions.Visibility;

/// <summary>テナント境界に基づく Action 利用可否を判定する。</summary>
public interface IActionVisibilityResolver
{
    /// <summary>指定テナントが当該 Action を参照・実行できるか。</summary>
    /// <param name="tenantId"><c>tenants.tenant_id</c> UUID 文字列。</param>
    /// <param name="descriptor">対象 Descriptor。</param>
    bool CanUse(string tenantId, ActionDescriptor descriptor);
}
