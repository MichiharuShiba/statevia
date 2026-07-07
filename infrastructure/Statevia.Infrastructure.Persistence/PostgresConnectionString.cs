namespace Statevia.Infrastructure.Persistence;

/// <summary>EF CLI 等で使う PostgreSQL 接続文字列の正規化。</summary>
internal static class PostgresConnectionString
{
    /// <summary>
    /// <paramref name="value"/> が <c>postgres://</c> / <c>postgresql://</c> なら Npgsql 形式へ変換する。
    /// </summary>
    internal static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            ? NormalizePostgresUrl(value)
            : value;
    }

    private static string NormalizePostgresUrl(string url)
    {
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
