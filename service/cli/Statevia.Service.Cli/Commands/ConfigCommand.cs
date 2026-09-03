using Statevia.Service.Cli.Infrastructure;
using System.CommandLine;

namespace Statevia.Service.Cli.Commands;

/// <summary><c>statevia config set</c> / <c>get</c> / <c>unset</c> サブコマンド。</summary>
public static class ConfigCommand
{
    internal const string UnsetDisplay = "(unset)";
    internal const string ApiKeyMask = "********";

    /// <summary>コマンド定義を生成する。</summary>
    public static Command Create()
    {
        var command = new Command("config", "Read and write CLI home settings");
        command.AddCommand(CreateSet());
        command.AddCommand(CreateGet());
        command.AddCommand(CreateUnset());
        return command;
    }

    private static Command CreateSet()
    {
        var keyArgument = new Argument<string?>("key", "api-base, tenant, modules-path, or api-key. Omit to prompt each key")
        {
            Arity = ArgumentArity.ZeroOrOne,
        };
        var valueArgument = new Argument<string?>("value", "Value. Omit to prompt (Enter keeps the current value)")
        {
            Arity = ArgumentArity.ZeroOrOne,
        };
        var set = new Command("set", "Write a setting to STATEVIA_HOME")
        {
            keyArgument,
            valueArgument,
        };
        set.SetHandler(SetAsync, keyArgument, valueArgument);
        return set;
    }

    private static Command CreateGet()
    {
        var keyArgument = new Argument<string?>("key", "Optional setting key")
        {
            Arity = ArgumentArity.ZeroOrOne,
        };
        var get = new Command("get", "Print effective setting values")
        {
            keyArgument,
        };
        get.SetHandler(GetAsync, keyArgument);
        return get;
    }

    private static Command CreateUnset()
    {
        var keyArgument = new Argument<string>("key", "api-base, tenant, modules-path, or api-key");
        var unset = new Command("unset", "Remove a setting from STATEVIA_HOME")
        {
            keyArgument,
        };
        unset.SetHandler(UnsetAsync, keyArgument);
        return unset;
    }

    private static async Task<int> SetAsync(string? key, string? value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    await Console.Error.WriteLineAsync(UnknownKeyMessage(value)).ConfigureAwait(false);
                    return 1;
                }

                await Console.Out.WriteLineAsync("Press Enter to keep the current value.")
                    .ConfigureAwait(false);
                return await PromptAllAsync().ConfigureAwait(false);
            }

            var normalized = NormalizeKey(key);
            if (normalized is null)
            {
                await Console.Error.WriteLineAsync(UnknownKeyMessage(key)).ConfigureAwait(false);
                return 1;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                await Console.Out.WriteLineAsync("Press Enter to keep the current value.")
                    .ConfigureAwait(false);
                return await PromptOneAsync(normalized.Value).ConfigureAwait(false);
            }

            return await ApplyValueAsync(normalized.Value, value, keepIfEmpty: false).ConfigureAwait(false);
        }
        catch (CliHomeFileException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<int> PromptAllAsync()
    {
        var next = LoadConfigOrEmpty();
        foreach (var key in new[] { ConfigKey.ApiBase, ConfigKey.Tenant, ConfigKey.ModulesPath, ConfigKey.ApiKey })
        {
            var current = CurrentStored(key, next);
            var entered = await CliConsolePrompt.ReadAsync(KeyName(key), current, secret: key == ConfigKey.ApiKey)
                .ConfigureAwait(false);
            var applied = await TryAssignAsync(next, key, entered, allowEmpty: true).ConfigureAwait(false);
            if (applied is null)
                return 1;
            next = applied;
        }

        new CliConfigStore().Save(next);
        return 0;
    }

    private static async Task<int> PromptOneAsync(ConfigKey key)
    {
        var currentConfig = LoadConfigOrEmpty();
        var current = CurrentStored(key, currentConfig);
        var entered = await CliConsolePrompt.ReadAsync(KeyName(key), current, secret: key == ConfigKey.ApiKey)
            .ConfigureAwait(false);
        return await ApplyValueAsync(key, entered, keepIfEmpty: true).ConfigureAwait(false);
    }

    private static async Task<int> ApplyValueAsync(ConfigKey key, string? value, bool keepIfEmpty)
    {
        if (key == ConfigKey.ApiKey)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (keepIfEmpty)
                    return 0;
                await Console.Error.WriteLineAsync("API key is required.").ConfigureAwait(false);
                return 1;
            }

            new CliSecretsStore().SaveApiKey(value);
            return 0;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            if (keepIfEmpty)
                return 0;
            await Console.Error.WriteLineAsync("Value is required.").ConfigureAwait(false);
            return 1;
        }

        if (key == ConfigKey.ApiBase && !Uri.TryCreate(value.Trim(), UriKind.Absolute, out _))
        {
            await Console.Error.WriteLineAsync("Invalid api-base URL.").ConfigureAwait(false);
            return 1;
        }

        var store = new CliConfigStore();
        var current = store.TryLoad() ?? new CliConfigFile();
        store.Save(Apply(current, key, value.Trim()));
        return 0;
    }

    private static async Task<CliConfigFile?> TryAssignAsync(
        CliConfigFile current,
        ConfigKey key,
        string? value,
        bool allowEmpty)
    {
        if (key == ConfigKey.ApiKey)
        {
            if (string.IsNullOrWhiteSpace(value))
                return current;

            new CliSecretsStore().SaveApiKey(value);
            return current;
        }

        if (string.IsNullOrWhiteSpace(value))
            return allowEmpty ? current : null;

        if (key == ConfigKey.ApiBase && !Uri.TryCreate(value.Trim(), UriKind.Absolute, out _))
        {
            await Console.Error.WriteLineAsync("Invalid api-base URL.").ConfigureAwait(false);
            return null;
        }

        return Apply(current, key, value.Trim());
    }

    private static CliConfigFile LoadConfigOrEmpty() =>
        new CliConfigStore().TryLoad() ?? new CliConfigFile();

    private static string? CurrentStored(ConfigKey key, CliConfigFile config) =>
        key switch
        {
            ConfigKey.ApiBase => config.ApiBase,
            ConfigKey.Tenant => config.Tenant,
            ConfigKey.ModulesPath => config.ModulesPath,
            ConfigKey.ApiKey => new CliSecretsStore().TryGetApiKey(),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, message: null),
        };

    private static string KeyName(ConfigKey key) =>
        key switch
        {
            ConfigKey.ApiBase => "api-base",
            ConfigKey.Tenant => "tenant",
            ConfigKey.ModulesPath => "modules-path",
            ConfigKey.ApiKey => "api-key",
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, message: null),
        };

    private static async Task<int> GetAsync(string? key)
    {
        try
        {
            var resolver = new CliSettingsResolver();
            if (string.IsNullOrWhiteSpace(key))
            {
                await WriteEffectiveLineAsync("api-base", resolver.ResolveApiBase(null)).ConfigureAwait(false);
                await WriteEffectiveLineAsync("tenant", resolver.ResolveTenant(null)).ConfigureAwait(false);
                await WriteEffectiveLineAsync("modules-path", resolver.ResolveModulesPath(null)).ConfigureAwait(false);
                await WriteEffectiveLineAsync("api-key", resolver.ResolveApiKey(null), mask: true).ConfigureAwait(false);
                return 0;
            }

            var normalized = NormalizeKey(key);
            if (normalized is null)
            {
                await Console.Error.WriteLineAsync(UnknownKeyMessage(key)).ConfigureAwait(false);
                return 1;
            }

            var value = Resolve(resolver, normalized.Value);
            await Console.Out.WriteLineAsync(FormatValue(value, mask: normalized.Value == ConfigKey.ApiKey))
                .ConfigureAwait(false);
            return 0;
        }
        catch (CliHomeFileException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<int> UnsetAsync(string key)
    {
        try
        {
            var normalized = NormalizeKey(key);
            if (normalized is null)
            {
                await Console.Error.WriteLineAsync(UnknownKeyMessage(key)).ConfigureAwait(false);
                return 1;
            }

            if (normalized == ConfigKey.ApiKey)
            {
                new CliSecretsStore().DeleteApiKey();
                return 0;
            }

            var store = new CliConfigStore();
            var current = store.TryLoad() ?? new CliConfigFile();
            store.Save(Apply(current, normalized.Value, value: null));
            return 0;
        }
        catch (CliHomeFileException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return 1;
        }
    }

    private static ConfigKey? NormalizeKey(string key) =>
        key.Trim().ToLowerInvariant() switch
        {
            "api-base" => ConfigKey.ApiBase,
            "tenant" => ConfigKey.Tenant,
            "modules-path" => ConfigKey.ModulesPath,
            "api-key" => ConfigKey.ApiKey,
            _ => null,
        };

    private static CliConfigFile Apply(CliConfigFile current, ConfigKey key, string? value) =>
        key switch
        {
            ConfigKey.ApiBase => current with { ApiBase = NullIfEmpty(value) },
            ConfigKey.Tenant => current with { Tenant = NullIfEmpty(value) },
            ConfigKey.ModulesPath => current with { ModulesPath = NullIfEmpty(value) },
            ConfigKey.ApiKey => current,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, message: null),
        };

    private static string? Resolve(CliSettingsResolver resolver, ConfigKey key) =>
        key switch
        {
            ConfigKey.ApiBase => resolver.ResolveApiBase(null),
            ConfigKey.Tenant => resolver.ResolveTenant(null),
            ConfigKey.ModulesPath => resolver.ResolveModulesPath(null),
            ConfigKey.ApiKey => resolver.ResolveApiKey(null),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, message: null),
        };

    private static async Task WriteEffectiveLineAsync(string key, string? value, bool mask = false) =>
        await Console.Out.WriteLineAsync($"{key}: {FormatValue(value, mask)}").ConfigureAwait(false);

    private static string FormatValue(string? value, bool mask)
    {
        if (string.IsNullOrWhiteSpace(value))
            return UnsetDisplay;

        return mask ? ApiKeyMask : value;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string UnknownKeyMessage(string key) =>
        $"Unknown key '{key}'. Use api-base, tenant, modules-path, or api-key.";

    private enum ConfigKey
    {
        ApiBase,
        Tenant,
        ModulesPath,
        ApiKey,
    }
}
