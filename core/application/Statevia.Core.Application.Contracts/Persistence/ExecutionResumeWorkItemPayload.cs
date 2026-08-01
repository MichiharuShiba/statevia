namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// Resume / TimerFire ワーク項目の JSON ペイロード形状。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ExecutionWorkItemKinds.Resume"/> および
/// <see cref="ExecutionWorkItemKinds.TimerFire"/> の <c>Payload</c> にシリアライズする。
/// Event ingress・DelayWait スケジューラ・Worker が同一契約を共有する。
/// </para>
/// </remarks>
/// <param name="NodeId">再開対象の Wait ノード ID。</param>
/// <param name="EventName">Resume に渡すイベント名。</param>
/// <param name="IdempotencyKey">任意の冪等キー（未指定時は null）。</param>
public sealed record ExecutionResumeWorkItemPayload(
    string NodeId,
    string EventName,
    string? IdempotencyKey = null);
