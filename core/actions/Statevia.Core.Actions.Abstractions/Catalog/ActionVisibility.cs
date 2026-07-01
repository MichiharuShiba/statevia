namespace Statevia.Core.Actions.Abstractions.Catalog;

/// <summary>Action の利用可能範囲。Identity（actionId）とは分離する。</summary>
public enum ActionVisibility
{
    /// <summary>プラットフォーム組み込み。全テナント利用可。</summary>
    Builtin,

    /// <summary>単一テナント所有。<see cref="ActionDescriptor.OwnerTenantId"/> 一致時のみ利用可。</summary>
    Tenant,

    /// <summary>組織内共有（Phase 4）。</summary>
    Organization,

    /// <summary>Marketplace 公開（Phase 4）。全テナント利用可。</summary>
    Marketplace,
}
