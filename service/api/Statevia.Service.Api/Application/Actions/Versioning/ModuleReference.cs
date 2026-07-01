namespace Statevia.Service.Api.Application.Actions.Versioning;

/// <summary>imports（定義 DSL）で表明する Module 参照（版解決前・拡張可能）。</summary>
/// <remarks>
/// <para>
/// 利用版の表明のみを担い、具体版への解決は <see cref="ModuleVersionResolver"/> が compile 時に行う。
/// 将来は source / trustPolicy / requireSignature 等の属性を追加し得る拡張枠とする。
/// </para>
/// </remarks>
/// <param name="Alias">定義内での別名（State の <c>alias.action</c> 参照に対応）。</param>
/// <param name="ModuleId">参照先 Module の一意識別子。</param>
/// <param name="VersionRange">版レンジ式（例 <c>^1.2</c>）。空文字は最新安定版（LATEST）。</param>
internal sealed record ModuleReference(
    string Alias,
    string ModuleId,
    string VersionRange = "");
