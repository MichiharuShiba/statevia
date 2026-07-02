namespace Statevia.Infrastructure.Persistence;

/// <summary>definitions.slug 生成ヘルパー。</summary>
internal static class DefinitionSlug
{
    /// <summary>表示名と definition ID から tenant 内一意の slug を生成する。</summary>
    public static string FromName(Guid definitionId, string name)
    {
        var trimmed = name.Trim();
        var baseName = string.IsNullOrEmpty(trimmed) ? "definition" : trimmed;
        var suffix = definitionId.ToString("N")[..8];
        var combined = $"{baseName}-{suffix}";
        return combined.Length <= 128 ? combined : combined[..128];
    }
}
