namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>command_dedup テーブル（コマンド冪等制御）。</summary>
public class CommandDedupRow
{
    public required string DedupKey { get; set; }
    public required string Endpoint { get; set; }
    public required string IdempotencyKey { get; set; }

    /// <summary>リクエストボディのハッシュ（任意）。</summary>
    public string? RequestHash { get; set; }

    public int? StatusCode { get; set; }
    public string? ResponseBody { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
