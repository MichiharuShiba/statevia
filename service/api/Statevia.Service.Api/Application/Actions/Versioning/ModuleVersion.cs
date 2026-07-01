using System.Globalization;

namespace Statevia.Service.Api.Application.Actions.Versioning;

/// <summary>Module の SemVer 2.0 サブセット表現（major.minor.patch ＋任意の pre-release）。</summary>
/// <remarks>
/// <para>
/// version coexist（複数版共存）の解決で用いる比較・解析の最小実装。build metadata（<c>+</c> 以降）は
/// 解析時に無視する（SemVer 同様に優先順位へ影響させない）。
/// </para>
/// <para>
/// 比較は SemVer 優先順位規則に従う。すなわち major / minor / patch を数値比較し、pre-release を持つ版は
/// 持たない安定版より低い。pre-release 同士はドット区切り識別子単位で比較する（数値同士は数値比較、
/// 数値は非数値より低く、非数値同士は序数比較、識別子数が多い方が高い）。
/// </para>
/// </remarks>
internal sealed record ModuleVersion : IComparable<ModuleVersion>
{
    /// <summary>各数値コンポーネント（major / minor / patch）の最大桁数。</summary>
    /// <remarks>
    /// 9 桁（最大 999,999,999）に制限する。<see cref="int"/> 範囲（約 21 億）に収まり、
    /// レンジ解決での上限算出（<c>+ 1</c>）でも桁あふれしないため。現実の版番号で 9 桁を超えることはなく、
    /// 巨大入力による異常値・オーバーフローを境界で早期に弾く目的（node-semver の桁数制限に倣う）。
    /// </remarks>
    public const int MaxComponentDigits = 9;

    /// <summary>major 番号（0 以上）。</summary>
    public int Major { get; }

    /// <summary>minor 番号（0 以上）。</summary>
    public int Minor { get; }

    /// <summary>patch 番号（0 以上）。</summary>
    public int Patch { get; }

    /// <summary>pre-release 識別子（例 <c>rc.1</c>）。安定版では <see langword="null"/>。</summary>
    public string? PreRelease { get; }

    /// <summary>pre-release を持たない安定版なら <see langword="true"/>。</summary>
    public bool IsStable => string.IsNullOrEmpty(PreRelease);

    /// <summary>各コンポーネントを指定して生成する。</summary>
    /// <param name="major">major 番号（0 以上）。</param>
    /// <param name="minor">minor 番号（0 以上）。</param>
    /// <param name="patch">patch 番号（0 以上）。</param>
    /// <param name="preRelease">pre-release 識別子（任意）。空文字は安定版とみなす。</param>
    /// <exception cref="ArgumentOutOfRangeException">いずれかの番号が負のとき。</exception>
    public ModuleVersion(int major, int minor, int patch, string? preRelease = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(major);
        ArgumentOutOfRangeException.ThrowIfNegative(minor);
        ArgumentOutOfRangeException.ThrowIfNegative(patch);

        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = string.IsNullOrEmpty(preRelease) ? null : preRelease;
    }

    /// <summary>SemVer 文字列を解析する。失敗時は例外を投げる。</summary>
    /// <param name="value">解析対象（例 <c>1.2.3</c> / <c>1.3.0-rc.1</c>）。</param>
    /// <returns>解析結果。</returns>
    /// <exception cref="FormatException">SemVer サブセットとして解析できないとき。</exception>
    public static ModuleVersion Parse(string value) =>
        TryParse(value, out var version)
            ? version!
            : throw new FormatException($"'{value}' is not a valid module version.");

    /// <summary>SemVer 文字列の解析を試みる。</summary>
    /// <param name="value">解析対象。</param>
    /// <param name="version">解析結果（失敗時は <see langword="null"/>）。</param>
    /// <returns>解析できたら <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out ModuleVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        // build metadata（+ 以降）は優先順位に影響しないため除去する。
        var plusIndex = trimmed.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex >= 0)
        {
            trimmed = trimmed[..plusIndex];
        }

        string? preRelease = null;
        var dashIndex = trimmed.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex >= 0)
        {
            preRelease = trimmed[(dashIndex + 1)..];
            trimmed = trimmed[..dashIndex];
            if (preRelease.Length == 0)
            {
                return false;
            }
        }

        var parts = trimmed.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!TryParseComponent(parts[0], out var major)
            || !TryParseComponent(parts[1], out var minor)
            || !TryParseComponent(parts[2], out var patch))
        {
            return false;
        }

        version = new ModuleVersion(major, minor, patch, preRelease);
        return true;
    }

    /// <summary>数値コンポーネントを解析する（非負・先頭ゼロ禁止）。</summary>
    private static bool TryParseComponent(string text, out int value)
    {
        value = 0;
        if (text.Length is 0 or > MaxComponentDigits)
        {
            return false;
        }

        // 先頭ゼロ（"01" 等）は SemVer 違反。
        if (text.Length > 1 && text[0] == '0')
        {
            return false;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    /// <inheritdoc />
    public int CompareTo(ModuleVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byMajor = Major.CompareTo(other.Major);
        if (byMajor != 0)
        {
            return byMajor;
        }

        var byMinor = Minor.CompareTo(other.Minor);
        if (byMinor != 0)
        {
            return byMinor;
        }

        var byPatch = Patch.CompareTo(other.Patch);
        if (byPatch != 0)
        {
            return byPatch;
        }

        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    /// <summary>pre-release の優先順位を比較する（安定版＞pre-release）。</summary>
    private static int ComparePreRelease(string? left, string? right)
    {
        var leftStable = string.IsNullOrEmpty(left);
        var rightStable = string.IsNullOrEmpty(right);

        if (leftStable && rightStable)
        {
            return 0;
        }

        // 安定版（pre-release なし）の方が高い。
        if (leftStable)
        {
            return 1;
        }

        if (rightStable)
        {
            return -1;
        }

        var leftIds = left!.Split('.');
        var rightIds = right!.Split('.');
        var common = Math.Min(leftIds.Length, rightIds.Length);

        for (var i = 0; i < common; i++)
        {
            var comparison = CompareIdentifier(leftIds[i], rightIds[i]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        // 識別子数が多い方が高い。
        return leftIds.Length.CompareTo(rightIds.Length);
    }

    /// <summary>pre-release 識別子 1 件を比較する（数値＜非数値、数値同士は数値比較）。</summary>
    private static int CompareIdentifier(string left, string right)
    {
        var leftNumeric = int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftValue);
        var rightNumeric = int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightValue);

        return (leftNumeric, rightNumeric) switch
        {
            (true, true) => leftValue.CompareTo(rightValue),
            (true, false) => -1,
            (false, true) => 1,
            _ => string.CompareOrdinal(left, right),
        };
    }

    /// <inheritdoc />
    public override string ToString() =>
        IsStable
            ? $"{Major}.{Minor}.{Patch}"
            : $"{Major}.{Minor}.{Patch}-{PreRelease}";
}
