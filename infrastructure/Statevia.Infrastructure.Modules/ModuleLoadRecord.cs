using Statevia.Core.Actions.Abstractions.Catalog;

namespace Statevia.Infrastructure.Modules;

/// <summary>Module load の監査レコード。</summary>
internal sealed record ModuleLoadRecord
{
    /// <summary>Module ID（<see cref="IActionModule.ModuleId"/>）。</summary>
    public required string ModuleId { get; init; }

    /// <summary>表示名。</summary>
    public required string Name { get; init; }

    /// <summary>バージョン。</summary>
    public required string Version { get; init; }

    /// <summary>load 状態。</summary>
    public required ModuleLoadStatus Status { get; init; }

    /// <summary>entry DLL の SHA256（hex）。</summary>
    public required string Sha256 { get; init; }

    /// <summary>発見元ラベル。</summary>
    public string? SourceLabel { get; init; }

    /// <summary>load 完了 UTC。</summary>
    public required DateTimeOffset LoadedAtUtc { get; init; }

    /// <summary>状態説明（skip / failed 理由等）。</summary>
    public string? Message { get; init; }

    /// <summary>entry DLL の絶対パス。</summary>
    public required string EntryAssemblyPath { get; init; }

    /// <summary>load 成功時の Module メタデータ（Trust / Source / 署名枠）。</summary>
    public ModuleDescriptor? ModuleDescriptor { get; init; }
}
