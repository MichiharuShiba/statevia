namespace Statevia.Core.Api.Application.Actions.Versioning;

/// <summary>compile 時に版を確定した不変の Module 参照（Definition Version に保存される想定）。</summary>
/// <remarks>
/// <para>
/// <see cref="ModuleReference"/> を <see cref="ModuleVersionResolver"/> が解決した結果。Runtime は本参照の
/// <see cref="ResolvedVersion"/> のみを用いて Catalog を exact lookup し、再解決・フォールバックを一切行わない
/// （決定論的実行）。
/// </para>
/// </remarks>
/// <param name="Alias">定義内での別名。</param>
/// <param name="ModuleId">参照先 Module の一意識別子。</param>
/// <param name="ResolvedVersion">確定版（fullVersion = major.minor.patch）。</param>
internal sealed record ResolvedModuleReference(
    string Alias,
    string ModuleId,
    string ResolvedVersion);
