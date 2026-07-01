namespace Statevia.Service.Api.Bootstrap;

/// <summary>全サブコマンド共通の CLI 引数。</summary>
internal sealed class BootstrapGlobalCliOptions
{
    /// <summary>共通オプションなし。</summary>
    public static readonly BootstrapGlobalCliOptions Empty = new();

    /// <summary>ルートヘルプ表示。</summary>
    public bool ShowRootHelp { get; init; }

    /// <summary>PostgreSQL 接続（<c>postgres://</c> または Npgsql 形式）。</summary>
    public string? DatabaseUrl { get; init; }

    /// <summary>追加の JSON 設定ファイルパス。</summary>
    public string? ConfigPath { get; init; }

    /// <summary>先頭のグローバルオプションを除去し、コマンド引数を返す。</summary>
    public static (BootstrapGlobalCliOptions Global, string[] CommandArgs) Parse(string[] args)
    {
        string? databaseUrl = null;
        string? configPath = null;
        var showRootHelp = false;
        var index = 0;

        for (; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "-h":
                case "--help":
                    showRootHelp = true;
                    break;
                case "--database-url":
                case "--connection-string":
                    databaseUrl = CliArgReader.RequireValue(args, ref index, arg);
                    break;
                case "--config":
                    configPath = CliArgReader.RequireValue(args, ref index, arg);
                    break;
                default:
                    return (
                        new BootstrapGlobalCliOptions
                        {
                            ShowRootHelp = showRootHelp,
                            DatabaseUrl = databaseUrl,
                            ConfigPath = configPath
                        },
                        args[index..]);
            }
        }

        return (
            new BootstrapGlobalCliOptions
            {
                ShowRootHelp = showRootHelp,
                DatabaseUrl = databaseUrl,
                ConfigPath = configPath
            },
            []);
    }
}
