using System.Diagnostics.CodeAnalysis;
using Statevia.Actions.Abstractions.Catalog;

namespace Statevia.Core.Api.Application.Actions.Catalog;

/// <summary>プロセス内の actionId → Descriptor / 実行ファクトリ マップ。</summary>
internal sealed class InMemoryActionCatalog : IActionCatalog
{
    private sealed record StoredRegistration(ActionDescriptor Descriptor, ActionCatalogEntry Entry);

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
    public void Register(ActionDescriptor descriptor, ActionCatalogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ActionDescriptorInvariants.Validate(descriptor);

        if (entry.InProcessFactory is null)
        {
            throw new ArgumentException(
                $"Action '{descriptor.ActionId}': InProcessFactory is required in Phase 1.",
                nameof(entry));
        }

        var canonicalId = descriptor.ActionId.Trim();
        _byCanonicalId[canonicalId] = new StoredRegistration(descriptor, entry);

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
}
