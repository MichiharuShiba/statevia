using System.CommandLine;
using System.Net.Http.Headers;
using Statevia.Infrastructure.Modules;

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
            description: "Core-API base URL for optional reload (e.g. http://localhost:8080)");
        var bearerTokenOption = new Option<string?>(
            aliases: ["--token", "-t"],
            description: "Bearer token for reload API (tenant admin)");
        var tenantKeyOption = new Option<string>(
            aliases: ["--tenant", "-T"],
            description: "Tenant key (required). Installs under {modulesRoot}/{tenantKey}/ and sets X-Tenant-Id on reload")
        {
            IsRequired = true,
        };
        var skipReloadOption = new Option<bool>(
            aliases: ["--skip-reload"],
            description: "Skip POST /internal/modules/reload after install");

        var command = new Command("module", "Action Module utilities");
        var install = new Command("install", "Install an Action Module zip into a tenant modules directory")
        {
            zipArgument,
            modulesPathOption,
            apiBaseOption,
            bearerTokenOption,
            tenantKeyOption,
            skipReloadOption,
        };
        install.SetHandler(
            InstallAsync,
            zipArgument,
            modulesPathOption,
            apiBaseOption,
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
        string? bearerToken,
        string tenantKey,
        bool skipReload)
    {
        if (!zipFile.Exists)
        {
            await Console.Error.WriteLineAsync($"File not found: {zipFile.FullName}").ConfigureAwait(false);
            return 1;
        }

        string normalizedTenantKey;
        string tenantModulesRoot;
        try
        {
            normalizedTenantKey = TenantModulePath.NormalizeTenantKey(tenantKey);
            var contentRoot = Directory.GetCurrentDirectory();
            // CLI の明示 --modules-path を環境変数より優先する。
            var modulesRoot = string.IsNullOrWhiteSpace(modulesPath)
                ? ModulePathResolver.Resolve(
                    contentRoot,
                    Environment.GetEnvironmentVariable(ModulePathResolver.EnvironmentVariable),
                    configurationPath: null)
                : ModulePathResolver.Resolve(
                    contentRoot,
                    environmentPath: null,
                    configurationPath: modulesPath);
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

            if (string.IsNullOrWhiteSpace(apiBase))
            {
                await Console.Out.WriteLineAsync(
                        "Reload skipped: specify --api-base to call POST /internal/modules/reload.")
                    .ConfigureAwait(false);
                return 0;
            }

            return await ReloadModulesAsync(apiBase, bearerToken, normalizedTenantKey).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await Console.Error.WriteLineAsync($"Install failed: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<int> ReloadModulesAsync(string apiBase, string? bearerToken, string tenantKey)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            await Console.Error.WriteLineAsync("Reload requires --token (tenant admin bearer token).")
                .ConfigureAwait(false);
            return 1;
        }

        if (!Uri.TryCreate(apiBase, UriKind.Absolute, out var apiBaseUri))
        {
            await Console.Error.WriteLineAsync("Invalid --api-base URL.").ConfigureAwait(false);
            return 1;
        }

        var reloadUri = new Uri(apiBaseUri, "internal/modules/reload");
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken.Trim());
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
