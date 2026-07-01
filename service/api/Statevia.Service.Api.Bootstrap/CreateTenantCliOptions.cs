namespace Statevia.Service.Api.Bootstrap;

/// <summary>create-tenant コマンドの引数。</summary>
internal sealed class CreateTenantCliOptions
{
    /// <summary>ヘルプ表示。</summary>
    public bool ShowHelp { get; init; }

    /// <summary>ヘルプのみ。</summary>
    public bool IsHelpOnly { get; init; }

    /// <summary>テナントキー。</summary>
    public string TenantKey { get; init; } = "";

    /// <summary>表示名。</summary>
    public string? DisplayName { get; init; }

    /// <summary>既存ならスキップ。</summary>
    public bool SkipIfExists { get; init; }

    /// <summary>引数を解析する。</summary>
    public static CreateTenantCliOptions Parse(string[] args)
    {
        var tenantKey = "";
        string? displayName = null;
        var skipIfExists = false;
        var showHelp = false;
        var isHelpOnly = false;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "-h":
                case "--help":
                    showHelp = true;
                    isHelpOnly = true;
                    break;
                case "--tenant-key":
                    tenantKey = CliArgReader.RequireValue(args, ref index, arg);
                    break;
                case "--display-name":
                    displayName = CliArgReader.RequireValue(args, ref index, arg);
                    break;
                case "--skip-if-exists":
                    skipIfExists = true;
                    break;
                default:
                    showHelp = true;
                    break;
            }
        }

        if (!showHelp && string.IsNullOrWhiteSpace(tenantKey))
            showHelp = true;

        return new CreateTenantCliOptions
        {
            ShowHelp = showHelp,
            IsHelpOnly = isHelpOnly,
            TenantKey = tenantKey,
            DisplayName = displayName,
            SkipIfExists = skipIfExists
        };
    }
}
