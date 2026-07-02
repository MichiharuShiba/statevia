using System.Text.Json;
using Statevia.Infrastructure.Persistence;

namespace Statevia.Infrastructure.Security;

/// <summary>API キー検証結果。</summary>
/// <param name="Tenant">テナント行。</param>
/// <param name="Principal">Principal 行。</param>
/// <param name="EffectiveScopes">交差評価後の有効スコープ。</param>
internal sealed record ApiKeyValidationResult(
    TenantRow Tenant,
    PrincipalRow Principal,
    IReadOnlySet<string> EffectiveScopes);

/// <summary>API キー認証サービス。</summary>
internal interface IApiKeyAuthenticationService
{
    /// <summary>API キーを検証する。</summary>
    Task<ApiKeyValidationResult?> ValidateAsync(string plainApiKey, CancellationToken cancellationToken);
}

/// <inheritdoc />
internal sealed class ApiKeyAuthenticationService : IApiKeyAuthenticationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IPlatformDataAccess _platformDataAccess;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public ApiKeyAuthenticationService(IPlatformDataAccess platformDataAccess)
    {
        _platformDataAccess = platformDataAccess;
    }

    /// <inheritdoc />
    public async Task<ApiKeyValidationResult?> ValidateAsync(string plainApiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plainApiKey))
            return null;

        var keyHash = PasswordCredentialService.HashApiKey(plainApiKey);
        var keyPrefix = PasswordCredentialService.ApiKeyPrefix(plainApiKey);
        var lookup = await _platformDataAccess
            .FindApiKeyCredentialAsync(keyPrefix, keyHash, cancellationToken)
            .ConfigureAwait(false);

        if (lookup is null || !lookup.Principal.IsActive || lookup.Principal.DeletedAt is not null)
            return null;

        if (lookup.ApiKey.ExpiresAt is { } expiresAt && expiresAt <= DateTime.UtcNow)
            return null;

        var allowedScopes = ParseAllowedScopes(lookup.ApiKey.AllowedScopesJson);
        var expandedPermissions = await _platformDataAccess
            .ExpandPrincipalPermissionKeysAsync(lookup.Principal.PrincipalId, cancellationToken)
            .ConfigureAwait(false);
        var effectiveScopes = ApiKeyScopeEvaluator.IntersectEffectiveScopes(expandedPermissions, allowedScopes);

        await _platformDataAccess
            .TouchApiKeyLastUsedAsync(lookup.ApiKey.ApiKeyId, cancellationToken)
            .ConfigureAwait(false);

        return new ApiKeyValidationResult(lookup.Tenant, lookup.Principal, effectiveScopes);
    }

    private static string[] ParseAllowedScopes(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var scopes = JsonSerializer.Deserialize<string[]>(json, JsonOptions);
            return scopes ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
