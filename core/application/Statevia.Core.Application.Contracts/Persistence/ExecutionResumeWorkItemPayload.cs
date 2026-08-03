namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// <see cref="ExecutionResumeWorkItemPayload.Mode"/> の許容値。
/// </summary>
public static class ExecutionResumeWorkItemModes
{
    /// <summary>Wait ノードを <c>nodeId</c> + <c>eventName</c> で解消する。</summary>
    public const string Event = "event";

    /// <summary>所有喪失からの継続再開（Wait を消費しない）。</summary>
    public const string Recovery = "recovery";
}

/// <summary>
/// Resume ワーク項目の JSON ペイロード形状。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ExecutionWorkItemKinds.Resume"/> の <c>Payload</c> にシリアライズする。
/// Event ingress・DelayWait スケジューラ・ownership recovery スケジューラ・Worker が同一契約を共有する。
/// </para>
/// <para>
/// <see cref="ExecutionResumeWorkItemModes.Event"/> では <see cref="NodeId"/> と
/// <see cref="EventName"/> が必須。
/// <see cref="ExecutionResumeWorkItemModes.Recovery"/> では不要。
/// </para>
/// </remarks>
/// <param name="Mode"><see cref="ExecutionResumeWorkItemModes"/> のいずれか。</param>
/// <param name="NodeId">再開対象の Wait ノード ID（event 時必須）。</param>
/// <param name="EventName">Resume に渡すイベント名（event 時必須）。</param>
/// <param name="IdempotencyKey">任意の冪等キー（未指定時は null）。</param>
public sealed record ExecutionResumeWorkItemPayload(
    string Mode,
    string? NodeId = null,
    string? EventName = null,
    string? IdempotencyKey = null);
