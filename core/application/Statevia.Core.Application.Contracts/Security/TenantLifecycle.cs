namespace Statevia.Core.Application.Contracts.Security;

/// <summary>テナントのライフサイクル状態。</summary>
public enum TenantLifecycle
{
    /// <summary>通常利用可能。</summary>
    Active = 0,

    /// <summary>一時停止。</summary>
    Suspended = 1,

    /// <summary>論理終了（復帰不可）。</summary>
    Archived = 2
}
