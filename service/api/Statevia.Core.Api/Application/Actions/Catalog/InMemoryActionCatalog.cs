using System.Diagnostics.CodeAnalysis;
using Statevia.Actions.Abstractions.Catalog;
using Statevia.Core.Api.Application.Actions.Publication;
using ActionPublication = Statevia.Actions.Abstractions.Publication.ActionPublication;

namespace Statevia.Core.Api.Application.Actions.Catalog;

/// <summary>プロセス内の actionId → Descriptor / 実行ファクトリ / Capability メタデータ マップ。</summary>
internal sealed class InMemoryActionCatalog : IActionCatalog
{
    private sealed record StoredRegistration(
        ActionDescriptor Descriptor,
        ActionCatalogEntry Entry,
        ActionCapabilityMetadata? CapabilityMetadata,
        ActionPublication? Publication);

    private readonly Dictionary<string, StoredRegistration> _byCanonicalId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _aliasToCanonicalId = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool Exists(string actionId) => TryResolveCanonicalId(actionId, out _);

    /// <inheritdoc />
    public bool TryGetDescriptor(string actionId, [NotNullWhen(true)] out ActionDescriptor? descriptor)
    {
        descriptor = null;
        if (!TryResolveCanonicalId(actionId, out var canonicalId))
        {
            return false;
        }

        descriptor = _byCanonicalId[canonicalId].Descriptor;
        return true;
    }

    /// <inheritdoc />
    public bool TryGetRegistration(string actionId, [NotNullWhen(true)] out ActionRegistration? registration)
    {
        registration = null;
        if (!TryResolveCanonicalId(actionId, out var canonicalId))
        {
            return false;
        }

        var stored = _byCanonicalId[canonicalId];
        registration = new ActionRegistration(stored.Descriptor, stored.Entry);
        return true;
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

        var canonicalId = descriptor.ActionId.Trim();
        if (_byCanonicalId.ContainsKey(canonicalId))
        {
            throw new ArgumentException($"Action '{canonicalId}' is already registered.");
        }

        _byCanonicalId[canonicalId] = new StoredRegistration(descriptor, entry, capabilityMetadata, publication);

        if (entry.Aliases is { Count: > 0 })
        {
            foreach (var alias in entry.Aliases)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(alias);
                var trimmedAlias = alias.Trim();
                if (string.Equals(trimmedAlias, canonicalId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (_aliasToCanonicalId.TryGetValue(trimmedAlias, out var existingCanonical)
                    && !string.Equals(existingCanonical, canonicalId, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"Alias '{trimmedAlias}' is already registered for action '{existingCanonical}'.");
                }

                _aliasToCanonicalId[trimmedAlias] = canonicalId;
            }
        }
    }

    private bool TryResolveCanonicalId(string actionId, [NotNullWhen(true)] out string? canonicalId)
    {
        canonicalId = null;
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return false;
        }

        var trimmed = actionId.Trim();
        if (_byCanonicalId.ContainsKey(trimmed))
        {
            canonicalId = trimmed;
            return true;
        }

        if (_aliasToCanonicalId.TryGetValue(trimmed, out var resolved))
        {
            canonicalId = resolved;
            return true;
        }

        return false;
    }

    /// <summary>Capability メタデータを取得する。</summary>
    /// <param name="actionId">参照 actionId。</param>
    /// <param name="metadata">取得したメタデータ。</param>
    public bool TryGetCapabilityMetadata(
        string actionId,
        [NotNullWhen(true)] out ActionCapabilityMetadata? metadata)
    {
        metadata = null;
        if (!TryResolveCanonicalId(actionId, out var canonicalId))
        {
            return false;
        }

        metadata = _byCanonicalId[canonicalId].CapabilityMetadata;
        return metadata is not null;
    }

    /// <inheritdoc />
    public bool TryGetPublication(string actionId, [NotNullWhen(true)] out ActionPublication? publication)
    {
        publication = null;
        if (!TryResolveCanonicalId(actionId, out var canonicalId))
        {
            return false;
        }

        publication = _byCanonicalId[canonicalId].Publication;
        return publication is not null;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetRegisteredActionIds() =>
        _byCanonicalId.Keys.OrderBy(static id => id, StringComparer.Ordinal).ToList();
}
