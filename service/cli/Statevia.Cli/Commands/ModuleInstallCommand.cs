using System.CommandLine;
using System.Net.Http.Headers;
using Statevia.Modules;

namespace Statevia.Cli.Commands;

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
            getDefaultValue: () => "default",
            description: "Tenant key for reload API (X-Tenant-Id)");
        var skipReloadOption = new Option<bool>(
            aliases: ["--skip-reload"],
            description: "Skip POST /internal/modules/reload after install");

        var command = new Command("module", "Action Module utilities");
        var install = new Command("install", "Install an Action Module zip into the modules root")
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

        var contentRoot = Directory.GetCurrentDirectory();
        var modulesRoot = ModulePathResolver.Resolve(
            contentRoot,
            Environment.GetEnvironmentVariable(ModulePathResolver.EnvironmentVariable),
            modulesPath);

        try
        {
            var installedDirectory = ModuleZipInstaller.Install(zipFile.FullName, modulesRoot);
            await Console.Out.WriteLineAsync($"Installed module to: {installedDirectory}").ConfigureAwait(false);

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

            return await ReloadModulesAsync(apiBase, bearerToken, tenantKey).ConfigureAwait(false);
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
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantKey.Trim());

        using var response = await client.PostAsync(reloadUri, content: null).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await Console.Error.WriteLineAsync(
                    $"Reload failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}")
                .ConfigureAwait(false);
            return 1;
        }

        await Console.Out.WriteLineAsync("Module reload requested successfully.").ConfigureAwait(false);
        return 0;
    }
}
