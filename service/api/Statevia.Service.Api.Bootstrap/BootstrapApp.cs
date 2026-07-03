using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Infrastructure.Security;

namespace Statevia.Service.Api.Bootstrap;

/// <summary>ブートストラップ CLI エントリ。</summary>
internal static class BootstrapApp
{
    /// <summary>テスト専用。未設定時は <see cref="BootstrapServiceRegistration.BuildProvider"/> を使用する。</summary>
    internal static Func<ServiceProvider>? ServiceProviderFactory { get; set; }

    /// <summary>引数を解釈してサブコマンドを実行する。</summary>
    public static async Task<int> RunAsync(string[] args)
    {
        var (globalOptions, commandArgs) = BootstrapGlobalCliOptions.Parse(args);

        if (commandArgs.Length == 0)
        {
            await BootstrapHelp.WriteRootAsync(Console.Out).ConfigureAwait(false);
            return 0;
        }

        if (globalOptions.ShowRootHelp && commandArgs is ["-h"] or ["--help"])
        {
            await BootstrapHelp.WriteRootAsync(Console.Out).ConfigureAwait(false);
            return 0;
        }

        var command = commandArgs[0];
        var subcommandArgs = commandArgs[1..];

        if (command is "-h" or "--help")
        {
            await BootstrapHelp.WriteRootAsync(Console.Out).ConfigureAwait(false);
            return 0;
        }

        ServiceProvider provider;
        try
        {
            provider = ServiceProviderFactory?.Invoke()
                ?? BootstrapServiceRegistration.BuildProvider(globalOptions);
        }
        catch (Exception ex) when (ex is FileNotFoundException or ArgumentException)
        {
            await WriteBootstrapErrorAsync(ex).ConfigureAwait(false);
            return 1;
        }

        try
        {
            return command switch
            {
                "create-tenant" => await RunCreateTenantAsync(provider, subcommandArgs).ConfigureAwait(false),
                "create-admin" => await RunCreateAdminAsync(provider, subcommandArgs).ConfigureAwait(false),
                _ => await UnknownCommandAsync(command).ConfigureAwait(false)
            };
        }
        finally
        {
            await provider.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<int> UnknownCommandAsync(string command)
    {
        await Console.Error.WriteLineAsync($"Unknown command: {command}").ConfigureAwait(false);
        await BootstrapHelp.WriteRootAsync(Console.Error).ConfigureAwait(false);
        return 1;
    }

    private static async Task<int> RunCreateTenantAsync(IServiceProvider provider, string[] args)
    {
        var options = CreateTenantCliOptions.Parse(args);
        if (options.ShowHelp)
        {
            var helpWriter = options.IsHelpOnly ? Console.Out : Console.Error;
            await BootstrapHelp.WriteCreateTenantAsync(helpWriter).ConfigureAwait(false);
            return options.IsHelpOnly ? 0 : 1;
        }

        var bootstrap = provider.GetRequiredService<TenantBootstrap>();
        try
        {
            var result = await bootstrap.CreateTenantAsync(
                options.TenantKey,
                options.DisplayName,
                options.SkipIfExists,
                CancellationToken.None).ConfigureAwait(false);

            if (result.Created)
            {
                await Console.Out.WriteLineAsync(
                    $"Created tenant: tenantKey={result.TenantKey} displayName={result.DisplayName}")
                    .ConfigureAwait(false);
                await Console.Out.WriteLineAsync($"  tenantId={result.TenantId}").ConfigureAwait(false);
            }
            else
            {
                await Console.Out.WriteLineAsync(
                    $"Skipped (already exists): tenantKey={result.TenantKey} tenantId={result.TenantId}")
                    .ConfigureAwait(false);
            }

            return 0;
        }
        catch (ArgumentException ex)
        {
            await WriteBootstrapErrorAsync(ex).ConfigureAwait(false);
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            await WriteBootstrapErrorAsync(ex).ConfigureAwait(false);
            return 1;
        }
        catch (DbUpdateException ex)
        {
            await WriteBootstrapErrorAsync(ex).ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<int> RunCreateAdminAsync(IServiceProvider provider, string[] args)
    {
        var options = CreateAdminCliOptions.Parse(args);
        if (options.ShowHelp)
        {
            var helpWriter = options.IsHelpOnly ? Console.Out : Console.Error;
            await BootstrapHelp.WriteCreateAdminAsync(helpWriter).ConfigureAwait(false);
            return options.IsHelpOnly ? 0 : 1;
        }

        var password = options.Password ?? Environment.GetEnvironmentVariable("STATEVIA_BOOTSTRAP_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            await Console.Error.WriteLineAsync("Error: set --password or STATEVIA_BOOTSTRAP_PASSWORD.")
                .ConfigureAwait(false);
            await BootstrapHelp.WriteCreateAdminAsync(Console.Error).ConfigureAwait(false);
            return 1;
        }

        var bootstrap = provider.GetRequiredService<TenantAdminBootstrap>();
        try
        {
            var result = await bootstrap.CreateTenantAdminAsync(
                options.TenantKey,
                options.Email,
                password,
                options.DisplayName,
                options.SkipIfExists,
                CancellationToken.None).ConfigureAwait(false);

            if (result.Created)
            {
                await Console.Out.WriteLineAsync(
                    $"Created tenant admin: tenantKey={result.TenantKey} email={result.Email}")
                    .ConfigureAwait(false);
                await Console.Out.WriteLineAsync($"  tenantId={result.TenantId}").ConfigureAwait(false);
                await Console.Out.WriteLineAsync($"  userId={result.UserId}").ConfigureAwait(false);
                await Console.Out.WriteLineAsync($"  principalId={result.PrincipalId}").ConfigureAwait(false);
            }
            else
            {
                await Console.Out.WriteLineAsync(
                    $"Skipped (already exists): tenantKey={result.TenantKey} email={result.Email} principalId={result.PrincipalId}")
                    .ConfigureAwait(false);
            }

            return 0;
        }
        catch (ArgumentException ex)
        {
            await WriteBootstrapErrorAsync(ex).ConfigureAwait(false);
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            await WriteBootstrapErrorAsync(ex).ConfigureAwait(false);
            return 1;
        }
        catch (DbUpdateException ex)
        {
            await WriteBootstrapErrorAsync(ex).ConfigureAwait(false);
            return 1;
        }
    }

    private static Task WriteBootstrapErrorAsync(Exception ex) =>
        Console.Error.WriteLineAsync($"Error: {ex.Message}");
}
