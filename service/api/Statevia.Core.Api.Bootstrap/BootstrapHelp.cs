namespace Statevia.Core.Api.Bootstrap;

/// <summary>CLI ヘルプ出力。</summary>
internal static class BootstrapHelp
{
    /// <summary>ルートヘルプ。</summary>
    public static Task WriteRootAsync(TextWriter writer) =>
        writer.WriteLineAsync(
            """
            Statevia platform bootstrap CLI

            Usage:
              dotnet run --project service/api/Statevia.Core.Api.Bootstrap -- [options] <command> [command options]

            Global options (before command):
              --database-url <url>       PostgreSQL URL or Npgsql connection string (overrides DATABASE_URL)
              --connection-string <cs>   Alias for --database-url
              --config <path>            Additional JSON settings (e.g. service/api/Statevia.Core.Api/appsettings.json)
              -h, --help                 Show help

            Commands:
              create-tenant   Create a tenants row (tenant_key is immutable)
              create-admin    Create tenant admin (Principal + User + user_principals)

            Configuration (lowest to highest priority):
              ./appsettings.json, ./appsettings.Development.json (optional, current directory)
              --config file
              environment variables (ConnectionStrings__DefaultConnection, DATABASE_URL)
              --database-url / --connection-string

            Run '<command> --help' for command options.
            """);

    /// <summary>create-tenant ヘルプ。</summary>
    public static Task WriteCreateTenantAsync(TextWriter writer) =>
        writer.WriteLineAsync(
            """
            Usage: ... [global options] create-tenant [options]

            Global options: --database-url, --connection-string, --config

            Options:
              --tenant-key <key>      Tenant key (required). lowercase, digits, hyphens, dots
              --display-name <name>   Display name (default: tenant-key)
              --skip-if-exists        No-op when tenant_key already exists
              -h, --help              Show help
            """);

    /// <summary>create-admin ヘルプ。</summary>
    public static Task WriteCreateAdminAsync(TextWriter writer) =>
        writer.WriteLineAsync(
            """
            Usage: ... [global options] create-admin [options]

            Global options: --database-url, --connection-string, --config

            Options:
              --tenant-key <key>      Tenant key (required)
              --email <email>         Admin email (required)
              --password <plain>      Password (prefer STATEVIA_BOOTSTRAP_PASSWORD)
              --display-name <name>   Principal display name (default: email)
              --skip-if-exists        No-op when login-ready user already exists
              -h, --help              Show help

            Environment:
              STATEVIA_BOOTSTRAP_PASSWORD   Plain password if --password omitted
            """);
}
