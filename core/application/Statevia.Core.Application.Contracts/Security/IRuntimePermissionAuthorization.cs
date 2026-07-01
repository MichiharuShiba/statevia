namespace Statevia.Core.Application.Contracts.Security;

/// <summary>Runtime API の global permission 評価。</summary>
public interface IRuntimePermissionAuthorization
{
    /// <summary>指定 semantic key を Principal が保持していることを要求する。</summary>
    Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken);
}
