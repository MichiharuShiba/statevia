using Statevia.Actions.Abstractions.Publication;

namespace Statevia.Service.Api.Application.Actions.Publication;

/// <summary>ActionUiMetadata の labelKey プレフィックスを登録時に検証する。</summary>
internal static class ActionUiMetadataValidator
{
    private const string UiFieldsSegment = ".ui.fields.";

    /// <summary>Publication の actionId と UiMetadata の i18n キー整合を検証する。</summary>
    /// <param name="canonicalActionId">Catalog の canonical actionId。</param>
    /// <param name="publication">検証対象 Publication。</param>
    /// <exception cref="ArgumentException">labelKey 等が canonical actionId 根で始まらない場合。</exception>
    public static void Validate(string canonicalActionId, ActionPublication publication)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalActionId);
        ArgumentNullException.ThrowIfNull(publication);

        var trimmedActionId = canonicalActionId.Trim();
        var publicationActionId = publication.Descriptor.ActionId.Trim();
        if (!string.Equals(trimmedActionId, publicationActionId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Publication Descriptor.ActionId '{publicationActionId}' does not match catalog actionId '{trimmedActionId}'.",
                nameof(publication));
        }

        if (publication.UiMetadata?.Fields is { Count: > 0 } fields)
        {
            foreach (var (fieldName, hints) in fields)
            {
                ValidateFieldKey(trimmedActionId, fieldName, hints.LabelKey, "labelKey");
                ValidateFieldKey(trimmedActionId, fieldName, hints.DescriptionKey, "descriptionKey");
                ValidateFieldKey(trimmedActionId, fieldName, hints.PlaceholderKey, "placeholderKey");
            }
        }

        if (publication.UiMetadata?.EnumLabelKeys is not { Count: > 0 } enumKeys)
        {
            return;
        }

        foreach (var (enumValue, labelKey) in enumKeys)
        {
            var context = $"enum '{enumValue}' labelKey";
            ValidateKeyPrefix(trimmedActionId, labelKey, context);
            ValidateStrictUiRoot(trimmedActionId, labelKey, context);
        }
    }

    private static void ValidateFieldKey(
        string canonicalActionId,
        string fieldName,
        string? key,
        string keyKind)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        ValidateKeyPrefix(canonicalActionId, key, $"field '{fieldName}' {keyKind}");

        var fieldSegment = $"{UiFieldsSegment}{fieldName}.";
        if (!key.Contains(fieldSegment, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"UiMetadata {keyKind} '{key}' must contain '{fieldSegment}' for field '{fieldName}'.",
                keyKind);
        }

        var expectedKey = $"{canonicalActionId}{fieldSegment}{ResolveFieldKeySuffix(keyKind)}";
        if (!string.Equals(key, expectedKey, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"UiMetadata {keyKind} '{key}' must exactly match '{expectedKey}'.",
                keyKind);
        }
    }

    private static string ResolveFieldKeySuffix(string keyKind) =>
        keyKind switch
        {
            "labelKey" => "label",
            "descriptionKey" => "description",
            "placeholderKey" => "placeholder",
            _ => throw new ArgumentException($"Unsupported field key kind '{keyKind}'.", nameof(keyKind)),
        };

    private static void ValidateKeyPrefix(string canonicalActionId, string key, string context)
    {
        var expectedPrefix = canonicalActionId + ".";
        if (!key.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{context} key '{key}' must start with canonical actionId prefix '{expectedPrefix}'.",
                nameof(key));
        }
    }

    /// <summary>actionId 直後が <c>.ui.</c> であることを検証する（余分なセグメントを拒否）。</summary>
    private static void ValidateStrictUiRoot(string canonicalActionId, string key, string context)
    {
        var expectedUiRoot = canonicalActionId + ".ui.";
        if (!key.StartsWith(expectedUiRoot, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{context} key '{key}' must start with '{expectedUiRoot}' immediately after canonical actionId.",
                nameof(key));
        }
    }
}
