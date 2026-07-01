namespace Statevia.Service.ActionHost;

/// <summary>Action Host の設定。</summary>
public sealed class ActionHostOptions
{
    /// <summary>設定セクション名。</summary>
    public const string SectionName = "Statevia:ActionHost";

    /// <summary>
    /// modules ルート（未設定時は <see cref="Statevia.Modules.ModulePathResolver"/> で解決）。
    /// </summary>
    public string? ModulesPath { get; set; }

    /// <summary>gRPC 待受 URL（未設定時は Kestrel 既定）。</summary>
    public string? ListenUrl { get; set; }
}
