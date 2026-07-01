namespace Statevia.Service.Api.Persistence;

/// <summary>execution_waits テーブル。durable wait のみ永続化する。</summary>
internal class ExecutionWaitRow
{
    /// <summary>execution 行の PK。</summary>
    public Guid ExecutionId { get; set; }

    /// <summary>実行グラフ上のノード ID。</summary>
    public required string NodeId { get; set; }

    /// <summary>wait の種別。</summary>
    public ExecutionWaitKind WaitKind { get; set; }

    /// <summary>Resume / PublishEvent で照合するトークン（Engine waitKey と一致）。</summary>
    public required string ResumeToken { get; set; }

    /// <summary>期限（EventWait は null）。</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>作成日時。</summary>
    public DateTime CreatedAt { get; set; }
}
