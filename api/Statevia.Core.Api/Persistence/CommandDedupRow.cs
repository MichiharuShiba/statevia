namespace Statevia.Core.Api.Persistence;

/// <summary>command_dedup テーブル（コマンド冪等制御）。</summary>
public class CommandDedupRow
{
    public required string DedupKey { get; set; }
    public required string Endpoint { get; set; }
    public required string IdempotencyKey { get; set; }

    // TODO: request_hash は将来的にリクエストボディのハッシュで埋める。
    public string? RequestHash { get; set; }

    // TODO: status_code / response_body は冪等レスポンス再利用の実装時に設定する。
    public int? StatusCode { get; set; }
    public string? ResponseBody { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

