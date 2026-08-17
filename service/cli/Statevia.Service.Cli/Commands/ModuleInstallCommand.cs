using System.CommandLine;
using Statevia.Infrastructure.Modules;
using Statevia.Service.Cli.Infrastructure;

namespace Statevia.Service.Cli.Commands;

/// <summary><c>statevia module install</c> サブコマンド。</summary>
public static class ModuleInstallCommand
{
    /// <summary>コマンド定義を生成する。</summary>
    public static Command Create()
    {
        var zipArgument = new Argument<FileInfo>("zip-file", "Action Module zip archive");
        var modulesPathOption = new Option<string?>(
            aliases: ["--modules-path", "-m"],
            description: "Modules root (default: STATEVIA_MODULES_PATH or ./modules)");
        var apiBaseOption = new Option<string?>(
            aliases: ["--api-base", "-a"],
            description: "Service API base URL for optional reload (e.g. http://localhost:8080)");
        var apiKeyOption = new Option<string?>(
            aliases: ["--api-key"],
            description: "API key for reload (X-Api-Key). Overrides STATEVIA_API_KEY");
        var bearerTokenOption = new Option<string?>(
            aliases: ["--token", "-t"],
            description: "Deprecated. Bearer token for reload. Prefer auth login or --api-key");
        var tenantKeyOption = new Option<string?>(
            aliases: ["--tenant", "-T"],
            description: "Tenant key. Falls back to STATEVIA_TENANT or home config. Installs under {modulesRoot}/{tenantKey}/ and sets X-Tenant-Id on reload");
        var skipReloadOption = new Option<bool>(
            aliases: ["--skip-reload"],
            description: "Skip POST /internal/modules/reload after install");

        var command = new Command("module", "Action Module utilities");
        var install = new Command("install", "Install an Action Module zip into a tenant modules directory")
        {
            zipArgument,
            modulesPathOption,
            apiBaseOption,
            apiKeyOption,
            bearerTokenOption,
            tenantKeyOption,
            skipReloadOption,
        };
        install.SetHandler(
            InstallAsync,
            zipArgument,
            modulesPathOption,
            apiBaseOption,
            apiKeyOption,
            bearerTokenOption,
            tenantKeyOption,
            skipReloadOption);
        command.AddCommand(install);
        return command;
    }

    private static async Task<int> InstallAsync(
        FileInfo zipFile,
        string? modulesPath,
        string? apiBase,
        string? apiKey,
        string? bearerToken,
        string? tenantKey,
        bool skipReload)
    {
        if (!zipFile.Exists)
        {
            await Console.Error.WriteLineAsync($"File not found: {zipFile.FullName}").ConfigureAwait(false);
            return 1;
        }

        string resolvedModulesPath;
        string resolvedApiBase;
        string resolvedTenantKey;
        try
        {
            var resolver = new CliSettingsResolver();
            resolvedModulesPath = resolver.ResolveModulesPath(modulesPath) ?? string.Empty;
            resolvedApiBase = resolver.ResolveApiBase(apiBase) ?? string.Empty;
            resolvedTenantKey = resolver.ResolveTenant(tenantKey) ?? string.Empty;
        }
        catch (CliHomeFileException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return 1;
        }

        if (string.IsNullOrWhiteSpace(resolvedTenantKey))
        {
            await Console.Error.WriteLineAsync(
                    "--tenant is required (or STATEVIA_TENANT / home config tenant).")
                .ConfigureAwait(false);
            return 1;
        }

        string normalizedTenantKey;
        string tenantModulesRoot;
        try
        {
            normalizedTenantKey = TenantModulePath.NormalizeTenantKey(resolvedTenantKey);
            var contentRoot = Directory.GetCurrentDirectory();
            var modulesRoot = string.IsNullOrWhiteSpace(resolvedModulesPath)
                ? ModulePathResolver.Resolve(
                    contentRoot,
                    environmentPath: null,
                    configurationPath: null)
                : ModulePathResolver.Resolve(
                    contentRoot,
                    environmentPath: null,
                    configurationPath: resolvedModulesPath);
            tenantModulesRoot = TenantModulePath.ResolveTenantModulesRoot(modulesRoot, normalizedTenantKey);
        }
        catch (ArgumentException ex)
        {
            await Console.Error.WriteLineAsync($"Invalid --tenant: {ex.Message}").ConfigureAwait(false);
            return 1;
        }

        try
        {
            var installedDirectory = ModuleZipInstaller.Install(zipFile.FullName, tenantModulesRoot);
            await Console.Out.WriteLineAsync(
                    $"Installed module for tenant '{normalizedTenantKey}' to: {installedDirectory}")
                .ConfigureAwait(false);

            if (skipReload)
            {
                return 0;
            }

            if (string.IsNullOrWhiteSpace(resolvedApiBase))
            {
                await Console.Out.WriteLineAsync(
                        "Reload skipped: specify --api-base to call POST /internal/modules/reload.")
                    .ConfigureAwait(false);
                return 0;
            }

            return await ReloadModulesAsync(resolvedApiBase, apiKey, bearerToken, normalizedTenantKey)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await Console.Error.WriteLineAsync($"Install failed: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<int> ReloadModulesAsync(
        string apiBase,
        string? apiKey,
        string? bearerToken,
        string tenantKey)
    {
        var store = new CliCredentialsStore();
        CliReloadAuth? auth;
        try
        {
            auth = CliReloadAuthentication.Resolve(apiKey, bearerToken, store, out var error);
            if (auth is null)
            {
                await Console.Error.WriteLineAsync(error ?? "Reload authentication failed.")
                    .ConfigureAwait(false);
                return 1;
            }
        }
        catch (CliHomeFileException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return 1;
        }

        if (auth.WarnTokenDeprecated)
        {
            await Console.Error.WriteLineAsync(
                    "Warning: --token is deprecated. Use 'statevia auth login' or --api-key / STATEVIA_API_KEY.")
                .ConfigureAwait(false);
        }

        if (!Uri.TryCreate(apiBase, UriKind.Absolute, out var apiBaseUri))
        {
            await Console.Error.WriteLineAsync("Invalid --api-base URL.").ConfigureAwait(false);
            return 1;
        }

        var reloadUri = new Uri(apiBaseUri, "internal/modules/reload");
        using var client = new HttpClient();
        CliReloadAuthentication.Apply(client, auth);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantKey);

        using var response = await client.PostAsync(reloadUri, content: null).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await Console.Error.WriteLineAsync(
                    $"Reload failed for tenant '{tenantKey}': HTTP {(int)response.StatusCode} {response.ReasonPhrase}. " +
                    "Module files may already be installed; retry reload or use --skip-reload.")
                .ConfigureAwait(false);
            return 1;
        }

        await Console.Out.WriteLineAsync(
                $"Module reload requested successfully for tenant '{tenantKey}' (HTTP {(int)response.StatusCode}).")
            .ConfigureAwait(false);
        return 0;
    }
}
