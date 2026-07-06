namespace Statevia.Core.Engine.Definition;

/// <summary>compile 時に確定した Module import（alias 単位・不変）。</summary>
/// <param name="ModuleId">参照先 Module の一意識別子。</param>
/// <param name="ResolvedVersion">確定版（fullVersion = major.minor.patch）。</param>
public sealed record ResolvedModuleBinding(string ModuleId, string ResolvedVersion);
