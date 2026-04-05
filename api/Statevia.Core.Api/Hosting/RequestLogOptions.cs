namespace Statevia.Core.Api.Hosting;

/// <summary>
/// HTTP リクエスト/レスポンス本文のログ方針。
/// </summary>
public sealed class RequestLogOptions
{
    /// <summary>リクエスト本文をログに載せる（本番既定 false 推奨）。</summary>
    public bool LogRequestBody { get; set; }

    /// <summary>レスポンス本文をログに載せる（本番既定 false 推奨）。</summary>
    public bool LogResponseBody { get; set; }

    /// <summary>リクエスト本文ログに読み込む上限バイト（超過分はログに載せない）。</summary>
    public int MaxRequestBodyLogBytes { get; set; } = 8192;

    /// <summary>レスポンス本文スナップショットの保持上限バイト。</summary>
    public int MaxResponseBodyLogBytes { get; set; } = 8192;

    /// <summary>開始ログのクエリ文字列の最大文字数（切り詰め）。</summary>
    public int MaxQueryStringChars { get; set; } = 2048;

    /// <summary>応答に解決済み traceId を <c>X-Trace-Id</c> として付与する。</summary>
    public bool EmitXTraceIdResponseHeader { get; set; } = true;

    /// <summary>ルートから得たドメイン ID を W3C <c>tracestate</c> のベンダーメンバーで応答に載せる。</summary>
    public bool EmitTracestateWithDomainIds { get; set; } = true;
}
