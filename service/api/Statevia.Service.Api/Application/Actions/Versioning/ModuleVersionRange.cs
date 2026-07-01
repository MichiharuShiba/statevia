using System.Globalization;

namespace Statevia.Service.Api.Application.Actions.Versioning;

/// <summary>imports で表明される版レンジ（npm / SemVer 準拠サブセット）。</summary>
/// <remarks>
/// <para>
/// 対応記法: 省略 / <c>LATEST</c>（最新安定版）、X-range（<c>1</c> / <c>1.2</c> / <c>1.x</c>）、
/// caret（<c>^1.2</c>）、tilde（<c>~1.2</c>）、exact（<c>1.2.3</c> / <c>=1.2.3</c> / <c>1.3.0-rc.1</c>）。
/// </para>
/// <para>
/// pre-release は exact 指定でのみ選択でき、それ以外のレンジは安定版のみを満たす（npm 準拠）。
/// 解決そのものは行わず、候補版がレンジを満たすか（<see cref="Satisfies"/>）の判定のみを担う。
/// </para>
/// </remarks>
internal sealed class ModuleVersionRange
{
    private readonly bool _isExact;
    private readonly ModuleVersion? _exact;
    private readonly ModuleVersion _minInclusive;
    private readonly ModuleVersion? _maxExclusive;

    private ModuleVersionRange(ModuleVersion exact)
    {
        _isExact = true;
        _exact = exact;
        _minInclusive = exact;
        _maxExclusive = null;
    }

    private ModuleVersionRange(ModuleVersion minInclusive, ModuleVersion? maxExclusive)
    {
        _isExact = false;
        _minInclusive = minInclusive;
        _maxExclusive = maxExclusive;
    }

    /// <summary>「最新安定版」を意味する無制限レンジ（省略 / LATEST 相当）か。</summary>
    public bool IsLatest => !_isExact && _minInclusive == new ModuleVersion(0, 0, 0) && _maxExclusive is null;

    /// <summary>exact 指定なら <see langword="true"/>（pre-release を選択し得る唯一の形）。</summary>
    public bool IsExact => _isExact;

    /// <summary>版レンジ文字列を解析する。失敗時は例外を投げる。</summary>
    /// <param name="expression">レンジ式（例 <c>^1.2</c> / <c>1.2</c> / <c>=1.2.3</c> / 空＝LATEST）。</param>
    /// <returns>解析結果。</returns>
    /// <exception cref="FormatException">レンジとして解析できないとき。</exception>
    public static ModuleVersionRange Parse(string? expression)
    {
        var text = expression?.Trim() ?? string.Empty;

        if (text.Length == 0
            || string.Equals(text, "LATEST", StringComparison.OrdinalIgnoreCase)
            || IsWildcard(text))
        {
            return new ModuleVersionRange(new ModuleVersion(0, 0, 0), maxExclusive: null);
        }

        return text[0] switch
        {
            '=' => ParseExact(text[1..]),
            '^' => ParseCaret(text[1..]),
            '~' => ParseTilde(text[1..]),
            _ => ParseBare(text),
        };
    }

    /// <summary>候補版がこのレンジを満たすか判定する。</summary>
    /// <param name="version">候補版。</param>
    /// <returns>満たすなら <see langword="true"/>。</returns>
    public bool Satisfies(ModuleVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        if (_isExact)
        {
            return version.Equals(_exact);
        }

        // exact 以外は pre-release を除外（npm 準拠）。
        if (!version.IsStable)
        {
            return false;
        }

        if (version.CompareTo(_minInclusive) < 0)
        {
            return false;
        }

        return _maxExclusive is null || version.CompareTo(_maxExclusive) < 0;
    }

    private static ModuleVersionRange ParseExact(string version)
    {
        if (!ModuleVersion.TryParse(version, out var parsed))
        {
            throw new FormatException($"'{version}' is not a valid exact version.");
        }

        return new ModuleVersionRange(parsed!);
    }

    private static ModuleVersionRange ParseCaret(string version)
    {
        var components = ParsePartial(version);

        // caret は最左の非ゼロ要素までを固定する（npm 準拠）。
        if (components.Major > 0 || components.SpecifiedCount == 1)
        {
            return new ModuleVersionRange(components.ToMin(), new ModuleVersion(components.Major + 1, 0, 0));
        }

        if (components.Minor > 0 || components.SpecifiedCount == 2)
        {
            return new ModuleVersionRange(components.ToMin(), new ModuleVersion(0, components.Minor + 1, 0));
        }

        return new ModuleVersionRange(components.ToMin(), new ModuleVersion(0, 0, components.Patch + 1));
    }

    private static ModuleVersionRange ParseTilde(string version)
    {
        var components = ParsePartial(version);

        // tilde は minor まで固定（major のみ指定時は major を固定）。
        if (components.SpecifiedCount >= 2)
        {
            return new ModuleVersionRange(components.ToMin(), new ModuleVersion(components.Major, components.Minor + 1, 0));
        }

        return new ModuleVersionRange(components.ToMin(), new ModuleVersion(components.Major + 1, 0, 0));
    }

    private static ModuleVersionRange ParseBare(string text)
    {
        // pre-release を含む（- を含む）完全版は exact 扱い。
        if (text.Contains('-', StringComparison.Ordinal))
        {
            return ParseExact(text);
        }

        var components = ParsePartial(text);

        return components switch
        {
            // 3 要素すべて数値指定（ワイルドカードなし）なら exact。
            { SpecifiedCount: 3, HasWildcard: false } =>
                new ModuleVersionRange(components.ToMin()),
            // major のみ → そのメジャー内（>=X.0.0 <(X+1).0.0）。
            { SpecifiedCount: 1 } =>
                new ModuleVersionRange(components.ToMin(), new ModuleVersion(components.Major + 1, 0, 0)),
            // major.minor → そのマイナー内（>=X.Y.0 <X.(Y+1).0）。
            _ =>
                new ModuleVersionRange(components.ToMin(), new ModuleVersion(components.Major, components.Minor + 1, 0)),
        };
    }

    /// <summary>部分指定版（<c>1</c> / <c>1.2</c> / <c>1.2.x</c> 等）を解析する。</summary>
    private static PartialVersion ParsePartial(string text)
    {
        if (text.Length == 0)
        {
            throw new FormatException("Version range is empty.");
        }

        var parts = text.Split('.');
        if (parts.Length is < 1 or > 3)
        {
            throw new FormatException($"'{text}' is not a valid version range.");
        }

        var major = 0;
        var minor = 0;
        var patch = 0;
        var specified = 0;
        var hasWildcard = false;

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (IsWildcard(part))
            {
                hasWildcard = true;
                break;
            }

            if (part.Length > ModuleVersion.MaxComponentDigits
                || !int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                throw new FormatException($"'{text}' contains an invalid version component.");
            }

            switch (i)
            {
                case 0:
                    major = number;
                    break;
                case 1:
                    minor = number;
                    break;
                default:
                    patch = number;
                    break;
            }

            specified++;
        }

        if (specified == 0)
        {
            throw new FormatException($"'{text}' does not specify a major version.");
        }

        return new PartialVersion(major, minor, patch, specified, hasWildcard);
    }

    private static bool IsWildcard(string part) => part is "x" or "X" or "*";

    /// <summary>部分指定された版コンポーネント。</summary>
    private readonly record struct PartialVersion(
        int Major,
        int Minor,
        int Patch,
        int SpecifiedCount,
        bool HasWildcard)
    {
        /// <summary>未指定要素を 0 で埋めた下限版を返す。</summary>
        public ModuleVersion ToMin() => new(Major, Minor, Patch);
    }
}
