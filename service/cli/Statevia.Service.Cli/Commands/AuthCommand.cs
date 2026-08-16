using System.CommandLine;
using System.Text;
using System.Text.Json;
using Statevia.Service.Cli.Infrastructure;

namespace Statevia.Service.Cli.Commands;

/// <summary><c>statevia auth login</c> / <c>logout</c> サブコマンド。</summary>
public static class AuthCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>コマンド定義を生成する。</summary>
    public static Command Create()
    {
        var command = new Command("auth", "CLI credential utilities");
        command.AddCommand(CreateLogin());
        command.AddCommand(CreateLogout());
        return command;
    }

    private static Command CreateLogin()
    {
        var apiBaseOption = new Option<string>(
            aliases: ["--api-base", "-a"],
            description: "Service API base URL (e.g. http://localhost:8080)")
        {
            IsRequired = true,
        };
        var tenantKeyOption = new Option<string>(
            aliases: ["--tenant", "-T"],
            description: "Tenant key")
        {
            IsRequired = true,
        };
        var emailOption = new Option<string>(
            aliases: ["--email", "-e"],
            description: "Login email")
        {
            IsRequired = true,
        };
        var passwordOption = new Option<string?>(
            aliases: ["--password", "-p"],
            description: "Login password (omit to prompt)");

        var login = new Command("login", "Sign in and save credentials to ~/.statevia/credentials")
        {
            apiBaseOption,
            tenantKeyOption,
            emailOption,
            passwordOption,
        };
        login.SetHandler(LoginAsync, apiBaseOption, tenantKeyOption, emailOption, passwordOption);
        return login;
    }

    private static Command CreateLogout()
    {
        var logout = new Command("logout", "Delete saved CLI credentials");
        logout.SetHandler(LogoutAsync);
        return logout;
    }

    private static async Task<int> LoginAsync(
        string apiBase,
        string tenantKey,
        string email,
        string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            await Console.Out.WriteAsync("Password: ").ConfigureAwait(false);
            password = await Console.In.ReadLineAsync().ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            await Console.Error.WriteLineAsync("Password is required.").ConfigureAwait(false);
            return 1;
        }

        if (!Uri.TryCreate(apiBase, UriKind.Absolute, out var apiBaseUri))
        {
            await Console.Error.WriteLineAsync("Invalid --api-base URL.").ConfigureAwait(false);
            return 1;
        }

        var loginUri = new Uri(apiBaseUri, "v1/auth/login");
        var requestJson = JsonSerializer.Serialize(
            new LoginRequestBody(tenantKey.Trim(), email.Trim(), password),
            JsonOptions);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(loginUri, content).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await Console.Error.WriteLineAsync(
                    $"Login failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}.")
                .ConfigureAwait(false);
            return 1;
        }

        var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        LoginResponseBody? payload;
        try
        {
            payload = JsonSerializer.Deserialize<LoginResponseBody>(responseJson, JsonOptions);
        }
        catch (JsonException)
        {
            await Console.Error.WriteLineAsync("Login failed: invalid response JSON.").ConfigureAwait(false);
            return 1;
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            await Console.Error.WriteLineAsync("Login failed: accessToken missing.").ConfigureAwait(false);
            return 1;
        }

        if (payload.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            await Console.Error.WriteLineAsync("Login failed: token already expired.").ConfigureAwait(false);
            return 1;
        }

        var savedTenantKey = string.IsNullOrWhiteSpace(payload.TenantKey)
            ? tenantKey.Trim()
            : payload.TenantKey;

        var store = new CliCredentialsStore();
        store.Save(new CliCredentials(
            savedTenantKey,
            payload.AccessToken,
            payload.ExpiresAt,
            apiBaseUri.ToString()));

        await Console.Out.WriteLineAsync($"Logged in. Credentials saved to {store.FilePath}.").ConfigureAwait(false);
        return 0;
    }

    private static Task LogoutAsync()
    {
        var store = new CliCredentialsStore();
        store.Delete();
        return Console.Out.WriteLineAsync($"Credentials removed ({store.FilePath}).");
    }

    private sealed record LoginRequestBody(string TenantKey, string Email, string Password);

    private sealed record LoginResponseBody(
        string AccessToken,
        DateTimeOffset ExpiresAt,
        Guid TenantId,
        string TenantKey,
        Guid PrincipalId);
}
