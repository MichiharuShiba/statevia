namespace Statevia.Service.Api.Hosting;

/// <summary>
/// PostgreSQL 接続文字列の解決（<c>DATABASE_URL</c> 正規化を含む）。
/// </summary>
internal static class DatabaseConnection
{
    /// <summary>
    /// 環境変数・設定から Npgsql 用接続文字列を解決する。
    /// </summary>
    /// <param name="configuration">アプリケーション設定。</param>
    /// <param name="connectionStringOverride">CLI 等で明示した接続文字列。指定時は環境変数より優先する。</param>
    /// <returns>Npgsql 接続文字列。</returns>
    public static string Resolve(IConfiguration configuration, string? connectionStringOverride = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!string.IsNullOrWhiteSpace(connectionStringOverride))
            return NormalizeConnectionString(connectionStringOverride.Trim());

        var rawDatabaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        return (string.IsNullOrWhiteSpace(rawDatabaseUrl) ? null : NormalizeConnectionString(rawDatabaseUrl))
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=statevia;Username=statevia;Password=statevia";
    }

    /// <summary>
    /// <c>postgres://</c> 形式なら正規化し、それ以外はそのまま返す。
    /// </summary>
    internal static string NormalizeConnectionString(string value) =>
        value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            ? NormalizePostgresUrl(value)
            : value;

    /// <summary>
    /// <c>postgres://</c> / <c>postgresql://</c> を Npgsql のキー=値形式へ変換する。
    /// </summary>
    internal static string NormalizePostgresUrl(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (!url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var uri = new Uri(url);
        var databaseName = uri.AbsolutePath.TrimStart('/');

        var userName = string.Empty;
        var password = string.Empty;
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            userName = Uri.UnescapeDataString(parts[0]);
            password = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
        }

        var port = uri.IsDefaultPort ? 5432 : uri.Port;
        return $"Host={uri.Host};Port={port};Database={databaseName};Username={userName};Password={password}";
    }
}
