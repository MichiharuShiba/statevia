using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Bootstrap;

/// <summary>create-admin コマンドの引数。</summary>
internal sealed class CreateAdminCliOptions
{
    /// <summary>ヘルプ表示。</summary>
    public bool ShowHelp { get; init; }

    /// <summary>ヘルプのみ。</summary>
    public bool IsHelpOnly { get; init; }

    /// <summary>テナントキー。</summary>
    public string TenantKey { get; init; } = TenantHeader.DefaultTenantId;

    /// <summary>メールアドレス。</summary>
    public string Email { get; init; } = "";

    /// <summary>平文パスワード。</summary>
    public string? Password { get; init; }

    /// <summary>Principal 表示名。</summary>
    public string? DisplayName { get; init; }

    /// <summary>既存ならスキップ。</summary>
    public bool SkipIfExists { get; init; }

    /// <summary>引数を解析する。</summary>
    public static CreateAdminCliOptions Parse(string[] args)
    {
        var tenantKey = TenantHeader.DefaultTenantId;
        var email = "";
        string? password = null;
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
                case "--email":
                    email = CliArgReader.RequireValue(args, ref index, arg);
                    break;
                case "--password":
                    password = CliArgReader.RequireValue(args, ref index, arg);
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

        if (!showHelp && string.IsNullOrWhiteSpace(email))
            showHelp = true;

        return new CreateAdminCliOptions
        {
            ShowHelp = showHelp,
            IsHelpOnly = isHelpOnly,
            TenantKey = tenantKey,
            Email = email,
            Password = password,
            DisplayName = displayName,
            SkipIfExists = skipIfExists
        };
    }
}
