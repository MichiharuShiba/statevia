using System.Diagnostics.CodeAnalysis;
using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Infrastructure.Modules.Catalog;
using Statevia.Infrastructure.Modules.Publication;
using ActionPublication = Statevia.Core.Actions.Abstractions.Publication.ActionPublication;

namespace Statevia.Service.Api.Application.Actions.Catalog;

/// <summary>プロセス内の版付き action キー → Descriptor / 実行ファクトリ マップ。</summary>
/// <remarks>
/// 内部キーは <see cref="VersionedActionKey"/>（moduleId + fullVersion + actionName）。
/// 論理 actionId（版なし）経由の lookup は、当該 Module のロード版がちょうど 1 つのときのみ成功する（Legacy 互換）。
/// </remarks>
internal sealed class InMemoryActionCatalog : IActionCatalog
{
    private sealed record StoredRegistration(
        ActionDescriptor Descriptor,
        ActionCatalogEntry Entry,
        ActionCapabilityMetadata? CapabilityMetadata,
        ActionPublication? Publication);

    private readonly Dictionary<VersionedActionKey, StoredRegistration> _byVersionedKey = [];
    private readonly Dictionary<string, List<VersionedActionKey>> _logicalActionIdIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _aliasToLogicalActionId = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool Exists(string actionId) => TryResolveLogicalActionId(actionId, out _);

    /// <inheritdoc />
    public bool TryGetDescriptor(string actionId, [NotNullWhen(true)] out ActionDescriptor? descriptor)
    {
        descriptor = null;
        if (!TryGetRegistration(actionId, out var registration))
        {
            return false;
        }

        descriptor = registration!.Descriptor;
        return descriptor is not null;
    }

    /// <inheritdoc />
    public bool TryGetDescriptor(
        string moduleId,
        string version,
        string actionName,
        [NotNullWhen(true)] out ActionDescriptor? descriptor)
    {
        descriptor = null;
        if (!TryGetRegistration(moduleId, version, actionName, out var registration))
        {
            return false;
        }

        descriptor = registration!.Descriptor;
        return descriptor is not null;
    }

    /// <inheritdoc />
    public bool TryGetRegistration(string actionId, [NotNullWhen(true)] out ActionRegistration? registration)
    {
        registration = null;
        if (!TryResolveLogicalActionId(actionId, out var logicalActionId))
        {
            return false;
        }

        if (!_logicalActionIdIndex.TryGetValue(logicalActionId, out var keys) || keys.Count == 0)
        {
            return false;
        }

        if (keys.Count > 1)
        {
            return false;
        }

        registration = ToRegistration(_byVersionedKey[keys[0]]);
        return true;
    }

    /// <inheritdoc />
    public bool TryGetRegistration(
        string moduleId,
        string version,
        string actionName,
        [NotNullWhen(true)] out ActionRegistration? registration)
    {
        registration = null;
        if (!TryCreateVersionedKey(moduleId, version, actionName, out var key))
        {
            return false;
        }

        if (!_byVersionedKey.TryGetValue(key, out var stored))
        {
            return false;
        }

        registration = ToRegistration(stored);
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetLoadedVersions(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        return _byVersionedKey.Keys
            .Where(key => string.Equals(key.ModuleId, moduleId.Trim(), StringComparison.Ordinal))
            .Select(key => key.Version)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static version => version, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public void Register(ActionDescriptor descriptor, ActionCatalogEntry entry) =>
        Register(descriptor, entry, capabilityMetadata: null, publication: null);

    /// <inheritdoc />
    public void Register(ActionDescriptor descriptor, ActionCatalogEntry entry, ActionPublication publication) =>
        Register(descriptor, entry, capabilityMetadata: null, publication);

    /// <summary>Capability メタデータ付きで Action を登録する。</summary>
    /// <param name="descriptor">Descriptor。</param>
    /// <param name="entry">実行エントリ。</param>
    /// <param name="capabilityMetadata">Capability メタデータ（任意）。</param>
    public void Register(
        ActionDescriptor descriptor,
        ActionCatalogEntry entry,
        ActionCapabilityMetadata? capabilityMetadata) =>
        Register(descriptor, entry, capabilityMetadata, publication: null);

    /// <summary>Capability メタデータと Publication 付きで Action を登録する。</summary>
    /// <param name="descriptor">Descriptor。</param>
    /// <param name="entry">実行エントリ。</param>
    /// <param name="capabilityMetadata">Capability メタデータ（任意）。</param>
    /// <param name="publication">Schema Publication（任意）。</param>
    public void Register(
        ActionDescriptor descriptor,
        ActionCatalogEntry entry,
        ActionCapabilityMetadata? capabilityMetadata,
        ActionPublication? publication)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ActionDescriptorInvariants.Validate(descriptor);

        if (entry.InProcessFactory is null)
        {
            throw new ArgumentException(
                $"Action '{descriptor.ActionId}': InProcessFactory is required in Phase 1.",
                nameof(entry));
        }

        if (publication is not null)
        {
            ActionUiMetadataValidator.Validate(descriptor.ActionId, publication);
        }

        var versionedKey = VersionedActionKey.FromDescriptor(descriptor);
        if (_byVersionedKey.ContainsKey(versionedKey))
        {
            throw new ArgumentException(
                $"Action '{versionedKey.LogicalActionId}' version '{versionedKey.Version}' is already registered.");
        }

        var stored = new StoredRegistration(descriptor, entry, capabilityMetadata, publication);
        _byVersionedKey[versionedKey] = stored;

        if (!_logicalActionIdIndex.TryGetValue(versionedKey.LogicalActionId, out var keys))
        {
            keys = [];
            _logicalActionIdIndex[versionedKey.LogicalActionId] = keys;
        }

        keys.Add(versionedKey);

        if (entry.Aliases is { Count: > 0 })
        {
            foreach (var alias in entry.Aliases)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(alias);
                var trimmedAlias = alias.Trim();
                if (string.Equals(trimmedAlias, versionedKey.LogicalActionId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (_aliasToLogicalActionId.TryGetValue(trimmedAlias, out var existingLogical)
                    && !string.Equals(existingLogical, versionedKey.LogicalActionId, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"Alias '{trimmedAlias}' is already registered for action '{existingLogical}'.");
                }

                _aliasToLogicalActionId[trimmedAlias] = versionedKey.LogicalActionId;
            }
        }
    }

    private bool TryResolveLogicalActionId(string actionId, [NotNullWhen(true)] out string? logicalActionId)
    {
        logicalActionId = null;
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return false;
        }

        var trimmed = actionId.Trim();
        if (_logicalActionIdIndex.ContainsKey(trimmed))
        {
            logicalActionId = trimmed;
            return true;
        }

        if (_aliasToLogicalActionId.TryGetValue(trimmed, out var resolved))
        {
            logicalActionId = resolved;
            return true;
        }

        return false;
    }

    private static bool TryCreateVersionedKey(
        string moduleId,
        string version,
        string actionName,
        out VersionedActionKey key)
    {
        key = default;
        if (string.IsNullOrWhiteSpace(moduleId)
            || string.IsNullOrWhiteSpace(version)
            || string.IsNullOrWhiteSpace(actionName))
        {
            return false;
        }

        var trimmedModuleId = moduleId.Trim();
        var trimmedVersion = version.Trim();
        var trimmedActionName = actionName.Trim();
        var logicalActionId = $"{trimmedModuleId}.{trimmedActionName}";
        key = new VersionedActionKey(trimmedModuleId, trimmedVersion, trimmedActionName, logicalActionId);
        return true;
    }

    private static ActionRegistration ToRegistration(StoredRegistration stored) =>
        new(stored.Descriptor, stored.Entry);

    /// <summary>Capability メタデータを取得する。</summary>
    /// <param name="actionId">参照 actionId。</param>
    /// <param name="metadata">取得したメタデータ。</param>
    public bool TryGetCapabilityMetadata(
        string actionId,
        [NotNullWhen(true)] out ActionCapabilityMetadata? metadata)
    {
        metadata = null;
        if (!TryResolveLogicalActionId(actionId, out var logicalActionId)
            || !_logicalActionIdIndex.TryGetValue(logicalActionId!, out var keys)
            || keys.Count != 1)
        {
            return false;
        }

        metadata = _byVersionedKey[keys[0]].CapabilityMetadata;
        return metadata is not null;
    }

    /// <inheritdoc />
    public bool TryGetPublication(string actionId, [NotNullWhen(true)] out ActionPublication? publication)
    {
        publication = null;
        if (!TryResolveLogicalActionId(actionId, out var logicalActionId)
            || !_logicalActionIdIndex.TryGetValue(logicalActionId!, out var keys)
            || keys.Count != 1)
        {
            return false;
        }

        publication = _byVersionedKey[keys[0]].Publication;
        return publication is not null;
    }

    /// <inheritdoc />
    public bool TryGetPublication(
        string moduleId,
        string version,
        string actionName,
        [NotNullWhen(true)] out ActionPublication? publication)
    {
        publication = null;
        if (!TryCreateVersionedKey(moduleId, version, actionName, out var key)
            || !_byVersionedKey.TryGetValue(key, out var stored))
        {
            return false;
        }

        publication = stored.Publication;
        return publication is not null;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetRegisteredActionIds() =>
        _logicalActionIdIndex.Keys.OrderBy(static id => id, StringComparer.Ordinal).ToList();
}
