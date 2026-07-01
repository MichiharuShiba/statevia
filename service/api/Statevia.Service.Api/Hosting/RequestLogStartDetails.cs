namespace Statevia.Service.Api.Hosting;

/// <summary>
/// HTTP リクエスト開始ログ用の構造化プロパティ。
/// </summary>
internal sealed class RequestLogStartDetails
{
    public required string TraceId { get; init; }

    public required string Method { get; init; }

    public required string Path { get; init; }

    public required string QueryForLog { get; init; }

    public Guid? TenantId { get; init; }

    public string? UserAgent { get; init; }

    public string? RequestBody { get; init; }
}
