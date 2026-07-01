namespace Statevia.Core.Api.Contracts.Admin;

/// <summary>GET /v1/admin/modules の 1 件。</summary>
public sealed record AdminModuleListItemDto
{
    /// <summary>Module ID。</summary>
    public required string ModuleId { get; init; }

    /// <summary>表示名。</summary>
    public required string Name { get; init; }

    /// <summary>バージョン。</summary>
    public required string Version { get; init; }

    /// <summary>load 状態（Loaded / Failed / Skipped / Duplicate）。</summary>
    public required string Status { get; init; }

    /// <summary>entry DLL の SHA256（hex）。</summary>
    public required string Sha256 { get; init; }

    /// <summary>発見元ラベル。</summary>
    public string? SourceLabel { get; init; }

    /// <summary>load 完了 UTC。</summary>
    public required DateTimeOffset LoadedAtUtc { get; init; }

    /// <summary>状態説明。</summary>
    public string? Message { get; init; }

    /// <summary>entry DLL の絶対パス。</summary>
    public required string EntryAssemblyPath { get; init; }
}
