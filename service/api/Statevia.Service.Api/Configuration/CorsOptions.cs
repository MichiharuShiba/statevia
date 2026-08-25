namespace Statevia.Service.Api.Configuration;

/// <summary>ブラウザから Service API へ直接アクセスするときの CORS 許可オリジン。</summary>
/// <remarks>
/// <para>Studio は Next.js の同一オリジンプロキシが正本であり、未設定時は ACAO を付けない。</para>
/// <para><c>AllowAnyOrigin</c> は使わない（Sonar S5122）。</para>
/// </remarks>
internal sealed class CorsOptions
{
    /// <summary>設定セクション名。</summary>
    public const string SectionName = "Statevia:Cors";

    /// <summary>
    /// 許可オリジン（スキーム + ホスト + ポート。末尾スラッシュなし）。空ならクロスオリジンを許可しない。
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];
}
