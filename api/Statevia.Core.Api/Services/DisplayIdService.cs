namespace Statevia.Core.Api.Services;

/// <summary>表示用 ID（10 桁・62 文字種）の生成と UUID 解決（U3）。</summary>
public interface IDisplayIdService
{
    /// <summary>新しい表示用 ID を生成し、DB に登録する。衝突時は再生成する。</summary>
    Task<string> AllocateAsync(string kind, Guid uuid, CancellationToken ct = default);

    /// <summary>表示用 ID または UUID 文字列から UUID を解決する。見つからなければ null。</summary>
    Task<Guid?> ResolveAsync(string kind, string idOrUuid, CancellationToken ct = default);

    /// <summary>表示用 ID を返す。idOrUuid が display_id 形式（UUID でない）なら検索せずそのまま返す。UUID 形式なら DB から display_id を取得し、見つからなければ null。</summary>
    Task<string?> GetDisplayIdAsync(string kind, string idOrUuid, CancellationToken ct = default);

    /// <summary>複数 resource_id の表示用 ID を 1 クエリで取得する。一覧 API の N+1 防止用。</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayIdsAsync(string kind, IEnumerable<Guid> resourceIds, CancellationToken ct = default);
}
