namespace Statevia.Service.Api.Persistence;

/// <summary>
/// execution_cursors テーブル（operational projection）。read-model の正本ではない。
/// </summary>
internal class ExecutionCursorRow
{
    /// <summary>execution 行の PK。</summary>
    public Guid ExecutionId { get; set; }

    /// <summary>移行期（project 未導入）のテナント境界。1b で project 経由に移行予定。</summary>
    public Guid TenantId { get; set; }

    /// <summary>現在アクティブな実行グラフノード ID（無ければ null）。</summary>
    public string? CurrentNodeId { get; set; }

    /// <summary>将来 RuntimeSpace 連携用。現フェーズでは未使用。</summary>
    public string? CurrentRuntimeId { get; set; }

    /// <summary>スケジュール中ノードの worker 識別子（無ければ null）。</summary>
    public string? CurrentWorkerId { get; set; }

    /// <summary>executions.status と同系統の実行状態（Running / Completed 等）。</summary>
    public required string State { get; set; }

    /// <summary>最終更新日時。</summary>
    public DateTime UpdatedAt { get; set; }
}
