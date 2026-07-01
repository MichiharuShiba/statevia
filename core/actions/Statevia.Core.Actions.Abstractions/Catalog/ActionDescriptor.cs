namespace Statevia.Core.Actions.Abstractions.Catalog;

/// <summary>Catalog に登録される Action の不変メタデータ。</summary>
public sealed record ActionDescriptor
{
    /// <summary>canonical actionId。</summary>
    public required string ActionId { get; init; }

    /// <summary>所属 Module ID（Builtin は <c>statevia.builtin</c> 等）。</summary>
    public required string ModuleId { get; init; }

    /// <summary>Action / Module のバージョン。</summary>
    public required string Version { get; init; }

    /// <summary>信頼レベル。</summary>
    public ActionTrustLevel TrustLevel { get; init; }

    /// <summary>導入元。</summary>
    public ActionSourceKind Source { get; init; }

    /// <summary>
    /// 所有者テナント（<c>tenants.tenant_id</c> UUID 文字列）。
    /// Builtin / Marketplace は <see langword="null"/>。
    /// </summary>
    public string? OwnerTenantId { get; init; }

    /// <summary>利用可能範囲。</summary>
    public ActionVisibility Visibility { get; init; }

    /// <summary>公開者（任意）。</summary>
    public ActionPublisher? Publisher { get; init; }

    /// <summary>署名（任意）。</summary>
    public ActionSignature? Signature { get; init; }

    /// <summary>実行ヒント。</summary>
    public ActionExecutionHints ExecutionHints { get; init; } = new();
}
