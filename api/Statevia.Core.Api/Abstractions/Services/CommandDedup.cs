namespace Statevia.Core.Api.Abstractions.Services;

/// <summary>
/// コマンド冪等（dedup）キーとエンドポイント情報の組。
/// </summary>
public readonly struct CommandDedupKey : IEquatable<CommandDedupKey>
{
    /// <summary>DB に保存する冪等キー文字列。</summary>
    public string DedupKey { get; init; }

    /// <summary>HTTP メソッドとパスから組み立てたエンドポイント識別子。</summary>
    public string Endpoint { get; init; }

    /// <summary>クライアントが送った <c>X-Idempotency-Key</c>（未送信時は空文字列などの正規化値）。</summary>
    public string IdempotencyKey { get; init; }

    /// <inheritdoc />
    public bool Equals(CommandDedupKey other) =>
        string.Equals(DedupKey, other.DedupKey, StringComparison.Ordinal)
        && string.Equals(Endpoint, other.Endpoint, StringComparison.Ordinal)
        && string.Equals(IdempotencyKey, other.IdempotencyKey, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is CommandDedupKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(DedupKey, Endpoint, IdempotencyKey);

    /// <summary>等値比較。</summary>
    public static bool operator ==(CommandDedupKey left, CommandDedupKey right) => left.Equals(right);

    /// <summary>非等値比較。</summary>
    public static bool operator !=(CommandDedupKey left, CommandDedupKey right) => !left.Equals(right);
}

/// <summary>
/// POST 等のコマンドに対する冪等キー生成と永続化の契約。
/// </summary>
public interface ICommandDedupService
{
    /// <summary>
    /// 冪等キーが有効なとき、dedup 行のキーとエンドポイント情報を生成する。不要なら <see langword="null"/>。
    /// </summary>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="idempotencyKey">リクエストの冪等キー（任意）。</param>
    /// <param name="method">HTTP メソッド。</param>
    /// <param name="path">リクエストパス（クエリなし）。</param>
    /// <param name="requestHash">リクエスト本文のハッシュ（任意）。</param>
    /// <returns>保存用の dedup 情報。冪等不要の場合は <see langword="null"/>。</returns>
    CommandDedupKey? Create(string tenantId, string? idempotencyKey, string method, string path, string? requestHash = null);
}
