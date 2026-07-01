namespace Statevia.Service.Api.Application.Actions.Versioning;

/// <summary>compile 時に版レンジを具体版（fullVersion）へ解決する（Compiler 配下の責務）。</summary>
/// <remarks>
/// <para>
/// 解決規則（npm 準拠）:
/// 省略 / LATEST はロード済みの最新安定版、X-range / caret / tilde はレンジを満たす最新安定版、
/// exact は当該版そのもの（pre-release を選べる唯一の形）へ解決する。pre-release は exact 以外では除外する。
/// </para>
/// <para>
/// 解決は compile 時に 1 回のみ実施し、結果（<see cref="ResolvedModuleReference"/>）は不変の Definition へ
/// 保存する想定。Runtime では再解決しない（決定論的実行）。一致版が無ければ
/// <see cref="ModuleVersionResolutionException"/> を投げ、暗黙の別版選択・近似フォールバックはしない。
/// </para>
/// </remarks>
internal static class ModuleVersionResolver
{
    /// <summary>版レンジを満たす最適版を選ぶ。</summary>
    /// <param name="versionRange">レンジ式（空＝最新安定版）。</param>
    /// <param name="availableVersions">対象 Module でロード済みの全版。</param>
    /// <returns>解決された具体版。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="availableVersions"/> が <see langword="null"/> のとき。</exception>
    /// <exception cref="FormatException">レンジ式が不正なとき。</exception>
    /// <exception cref="ModuleVersionResolutionException">レンジを満たす版が見つからないとき。</exception>
    public static ModuleVersion Resolve(string? versionRange, IEnumerable<ModuleVersion> availableVersions)
    {
        ArgumentNullException.ThrowIfNull(availableVersions);

        var range = ModuleVersionRange.Parse(versionRange);

        // exact 以外でも全件を比較するため一度実体化する。
        var candidates = availableVersions.Where(range.Satisfies).ToList();
        if (candidates.Count == 0)
        {
            throw new ModuleVersionResolutionException(
                $"No loaded module version satisfies range '{versionRange ?? string.Empty}'.");
        }

        return candidates.Max()!;
    }

    /// <summary>imports 参照を解決し、不変の確定参照を返す。</summary>
    /// <param name="reference">解決前の Module 参照。</param>
    /// <param name="availableVersions">対象 Module でロード済みの全版。</param>
    /// <returns>版を確定した <see cref="ResolvedModuleReference"/>。</returns>
    /// <exception cref="ArgumentNullException">いずれかの引数が <see langword="null"/> のとき。</exception>
    /// <exception cref="ModuleVersionResolutionException">レンジを満たす版が見つからないとき。</exception>
    public static ResolvedModuleReference Resolve(
        ModuleReference reference,
        IEnumerable<ModuleVersion> availableVersions)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var resolved = Resolve(reference.VersionRange, availableVersions);
        return new ResolvedModuleReference(reference.Alias, reference.ModuleId, resolved.ToString());
    }
}
