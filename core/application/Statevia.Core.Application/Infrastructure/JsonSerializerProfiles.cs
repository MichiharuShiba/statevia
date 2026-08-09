using System.Text.Json;

namespace Statevia.Core.Application.Infrastructure;

/// <summary>
/// Application 層の標準 <see cref="JsonSerializerOptions"/> プロファイル。
/// </summary>
/// <remarks>
/// <para>
/// camelCase シリアライズと、プロパティ名の大文字小文字非区別デシリアライズを
/// 別インスタンスとして提供する。設定を混ぜない（複合が必要な契約は専用 Options を使う）。
/// </para>
/// <para>
/// HTTP / DB 契約の正本ではなく、内部の参照共有とホットパス上の都度生成回避が目的。
/// </para>
/// </remarks>
internal static class JsonSerializerProfiles
{
    /// <summary>
    /// イベント本文・冪等キャッシュ応答・Fork 関連 JSON・SSE ペイロードなど、camelCase 出力向け。
    /// </summary>
    internal static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 歴史的に PascalCase が混在しうる投影／dedup 復元など、名前非区別デシリアライズ向け。
    /// </summary>
    internal static readonly JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
