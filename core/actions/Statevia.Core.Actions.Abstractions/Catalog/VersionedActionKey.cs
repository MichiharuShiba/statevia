namespace Statevia.Core.Actions.Abstractions.Catalog;

/// <summary>Catalog 登録・検索の版付きキー（moduleId + fullVersion + actionName）。</summary>
/// <param name="ModuleId">Module の一意識別子。</param>
/// <param name="Version">Module の fullVersion（major.minor.patch）。</param>
/// <param name="ActionName">Catalog 内部の Action 名（通常は論理 actionId の末尾セグメント）。</param>
/// <param name="LogicalActionId">版を含めない論理 actionId（canonical）。</param>
public readonly record struct VersionedActionKey(
    string ModuleId,
    string Version,
    string ActionName,
    string LogicalActionId)
{
    /// <summary><see cref="ActionDescriptor"/> から版付きキーを構築する。</summary>
    /// <param name="descriptor">Catalog 登録対象の Descriptor。</param>
    /// <returns>版付きキー。</returns>
    public static VersionedActionKey FromDescriptor(ActionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var logicalActionId = descriptor.ActionId.Trim();
        var modulePrefix = $"{descriptor.ModuleId}.";
        var actionName = logicalActionId.StartsWith(modulePrefix, StringComparison.Ordinal)
            ? logicalActionId[modulePrefix.Length..]
            : logicalActionId;

        if (actionName.Length == 0)
        {
            throw new ArgumentException(
                $"ActionId '{logicalActionId}' must include an action name.",
                nameof(descriptor));
        }

        return new VersionedActionKey(
            descriptor.ModuleId,
            descriptor.Version,
            actionName,
            logicalActionId);
    }
}
