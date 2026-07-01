namespace Statevia.Service.Api.Application.Security;

/// <summary>
/// Owner 経路の Resume / Cancel 認可が Snapshot か Live か。
/// Operator 経路には適用されない。
/// </summary>
public enum SecurityEvaluationMode
{
    /// <summary>Start 時点の <c>effectivePermissionKeys</c> を正とする。</summary>
    Snapshot = 0,

    /// <summary>現在の Principal 展開 permission を再評価する。</summary>
    Live = 1
}
