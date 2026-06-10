using System.Collections;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Engine.Definition;

/// <summary>
/// ワークフロー定義ローダーの共通基盤。
/// デシリアライズと Template Method の骨格を提供する。
/// </summary>
public abstract class WorkflowDefinitionLoaderBase : IDefinitionLoader
{
    private readonly IDeserializer _deserializer;

    /// <summary>
    /// デシリアライザを構築する。
    /// </summary>
    /// <param name="useScalarPreservingNodeTypeResolver">
    /// <see langword="true"/> のとき、スカラー型を保持するノード型リゾルバを有効にする。
    /// </param>
    protected WorkflowDefinitionLoaderBase(bool useScalarPreservingNodeTypeResolver = false)
    {
        var builder = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties();

        if (useScalarPreservingNodeTypeResolver)
        {
            builder = builder.WithNodeTypeResolver(new ScalarPreservingNodeTypeResolver());
        }

        _deserializer = builder.Build();
    }

    /// <inheritdoc />
    public WorkflowDefinition Load(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var raw = _deserializer.Deserialize<object>(new StringReader(content))
            ?? throw new InvalidOperationException("Invalid YAML/JSON");
        var root = ToStringDict(raw);
        return BuildDefinition(root);
    }

    /// <summary>デシリアライズ済みルートオブジェクトから定義を構築する。</summary>
    protected abstract WorkflowDefinition BuildDefinition(Dictionary<string, object?> root);

    /// <summary>
    /// 厳格仕様の input マッピングを解釈する。
    /// ${...} は拒否し、$. パスは単純 JSONPath 制約で検証する。
    /// </summary>
    protected static StateInputDefinition? ParseStrictInputMapping(object? inputVal, string? ownerLabel = null)
    {
        if (inputVal == null)
        {
            return null;
        }

        if (inputVal is string s)
        {
            return ParseStrictInputFromString(s, ownerLabel);
        }

        var map = ToStringDict(inputVal, StringComparer.OrdinalIgnoreCase);
        var singlePath = TryParseStrictInputSinglePath(map, ownerLabel);
        if (singlePath is not null)
        {
            return singlePath;
        }

        return ParseStrictInputFromMap(map, ownerLabel);
    }

    /// <summary>スカラー文字列の input を <see cref="StateInputDefinition"/> に解釈する。</summary>
    /// <param name="s">YAML/JSON 由来の文字列（パス式またはリテラル）。</param>
    /// <param name="ownerLabel">エラーメッセージ用のオーナー文脈。省略可。</param>
    /// <returns>単一 <c>path</c> または <c>values.value</c> リテラルとしての定義。</returns>
    private static StateInputDefinition ParseStrictInputFromString(string s, string? ownerLabel)
    {
        RejectTemplate(ownerLabel, s);
        if (IsPathExpression(s))
        {
            if (!SimpleJsonPath.IsValid(s))
            {
                throw new ArgumentException(Format(ownerLabel, $"invalid input path: '{s}'."));
            }

            return new StateInputDefinition { Path = s };
        }

        return new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["value"] = new StateInputValueDefinition { Literal = s }
            }
        };
    }

    /// <summary><c>{ path: "$...." }</c> 形式の単一キー input を解釈する。</summary>
    /// <param name="map">input の辞書。</param>
    /// <param name="ownerLabel">エラーメッセージ用のオーナー文脈。省略可。</param>
    /// <returns>単一 path として解釈できたときの定義。該当しないとき null。</returns>
    private static StateInputDefinition? TryParseStrictInputSinglePath(
        Dictionary<string, object?> map,
        string? ownerLabel)
    {
        if (map.Count != 1
            || !map.TryGetValue("path", out var pathVal)
            || pathVal is not string onlyPath)
        {
            return null;
        }

        RejectTemplate(ownerLabel, onlyPath);
        if (!IsPathExpression(onlyPath))
        {
            return null;
        }

        if (!SimpleJsonPath.IsValid(onlyPath))
        {
            throw new ArgumentException(Format(ownerLabel, $"invalid input.path: '{onlyPath}'."));
        }

        return new StateInputDefinition { Path = onlyPath };
    }

    /// <summary>複数キーの input マップを <c>values</c> 付き <see cref="StateInputDefinition"/> に解釈する。</summary>
    /// <param name="map">キーと値の input 辞書。</param>
    /// <param name="ownerLabel">エラーメッセージ用のオーナー文脈。省略可。</param>
    /// <returns>各キーを path または literal として解釈した定義。</returns>
    private static StateInputDefinition ParseStrictInputFromMap(
        Dictionary<string, object?> map,
        string? ownerLabel)
    {
        if (map.Keys.Any(key => string.Equals(key, "retry", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(Format(ownerLabel, "'retry' must not appear inside 'input'; declare it as a sibling of 'action'."));
        }

        var values = new Dictionary<string, StateInputValueDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, raw) in map)
        {
            values[key] = ParseStrictInputValue(raw, key, ownerLabel);
        }

        return new StateInputDefinition { Values = values };
    }

    /// <summary><c>workflow.modules</c> を syntax parse する（semantic resolution は行わない）。</summary>
    /// <param name="workflowDict"><c>workflow</c> ブロックの辞書。</param>
    /// <returns>module alias → ModuleId マップ。未指定時は null。</returns>
    protected static IReadOnlyDictionary<string, string>? ParseWorkflowModules(Dictionary<string, object?> workflowDict)
    {
        ArgumentNullException.ThrowIfNull(workflowDict);
        if (!workflowDict.TryGetValue("modules", out var modulesVal) || modulesVal is null)
        {
            return null;
        }

        var modulesDict = ToStringDict(modulesVal);
        if (modulesDict.Count == 0)
        {
            return null;
        }

        var entries = modulesDict
            .Select(entry => new KeyValuePair<string, string>(
                entry.Key,
                entry.Value?.ToString() ?? string.Empty))
            .ToList();

        var seenAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var modules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (aliasRaw, moduleIdRaw) in entries)
        {
            var alias = aliasRaw.Trim();
            if (alias.Length == 0)
            {
                throw new ArgumentException("workflow.modules contains an empty alias key.");
            }

            if (!seenAliases.Add(alias))
            {
                throw new ArgumentException($"workflow.modules contains duplicate alias '{alias}'.");
            }

            var moduleId = moduleIdRaw.Trim();
            if (moduleId.Length == 0)
            {
                throw new ArgumentException($"workflow.modules['{alias}'] requires a non-empty ModuleId.");
            }

            modules[alias] = moduleId;
        }

        return modules;
    }

    /// <summary>状態／ノード直下の <c>retry</c> ブロックを syntax parse する。</summary>
    /// <param name="dict">状態またはノード辞書。</param>
    /// <returns>retry 定義。未指定時は null。</returns>
    protected static RetryDefinition? ParseRetryDefinition(Dictionary<string, object?> dict)
    {
        ArgumentNullException.ThrowIfNull(dict);
        if (!dict.TryGetValue("retry", out var retryVal) || retryVal is null)
        {
            return null;
        }

        var retryDict = ToStringDict(retryVal);
        if (retryDict.Count == 0)
        {
            return null;
        }

        return new RetryDefinition
        {
            Limit = GetNullableInt(retryDict, "limit"),
            Backoff = GetStr(retryDict, "backoff"),
            Errors = GetStrList(retryDict, "errors"),
        };
    }

    /// <summary>input マップの 1 エントリを path または literal として解釈する。</summary>
    /// <param name="raw">YAML/JSON 由来の値。</param>
    /// <param name="key">マップ上のキー（エラーメッセージ用）。</param>
    /// <param name="ownerLabel">エラーメッセージ用のオーナー文脈。省略可。</param>
    /// <returns>path または literal を設定した値定義。</returns>
    private static StateInputValueDefinition ParseStrictInputValue(
        object? raw,
        string key,
        string? ownerLabel)
    {
        if (raw is not string str)
        {
            return new StateInputValueDefinition { Literal = raw };
        }

        RejectTemplate(ownerLabel, str);
        if (!IsPathExpression(str))
        {
            return new StateInputValueDefinition { Literal = str };
        }

        if (!SimpleJsonPath.IsValid(str))
        {
            throw new ArgumentException(Format(ownerLabel, $"invalid input path for key '{key}': '{str}'."));
        }

        return new StateInputValueDefinition { Path = str };
    }

    /// <summary>
    /// YAML の <c>when</c> オブジェクト（<c>path</c> / <c>op</c> / <c>value</c>）から条件式を構築する。
    /// <c>path</c> は <see cref="SimpleJsonPath.IsValid"/> で検証する。
    /// </summary>
    /// <param name="whenDict"><c>when</c> の辞書。</param>
    /// <param name="ownerLabel">エラーメッセージ用（例: nodes のノード id）。省略時はメッセージのみ。</param>
    /// <returns>構築した条件式。</returns>
    protected static ConditionExpressionDefinition ParseConditionWhen(
        Dictionary<string, object?> whenDict,
        string? ownerLabel = null)
    {
        ArgumentNullException.ThrowIfNull(whenDict);

        var path = GetStr(whenDict, "path");
        var op = GetStr(whenDict, "op");

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(Format(ownerLabel, "when requires non-empty 'path'."));
        }

        if (!SimpleJsonPath.IsValid(path))
        {
            throw new ArgumentException(Format(ownerLabel, $"invalid when.path: '{path}'."));
        }

        if (string.IsNullOrWhiteSpace(op))
        {
            throw new ArgumentException(Format(ownerLabel, "when requires non-empty 'op'."));
        }

        whenDict.TryGetValue("value", out var value);
        return new ConditionExpressionDefinition
        {
            Path = path,
            Op = op,
            Value = value
        };
    }

    /// <summary>
    /// 辞書の子キーに対応する値をネスト辞書として取得する。欠損・null のときは空辞書。
    /// </summary>
    /// <param name="dict">親辞書。</param>
    /// <param name="key">子を指すキー。</param>
    /// <param name="comparer">キー比較子。省略時は序数無視比較。</param>
    /// <returns>正規化した子辞書。</returns>
    protected static Dictionary<string, object?> GetChildDict(
        Dictionary<string, object?> dict,
        string key,
        IEqualityComparer<string>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(dict);
        if (!dict.TryGetValue(key, out var val) || val == null)
        {
            return new Dictionary<string, object?>(comparer ?? StringComparer.Ordinal);
        }

        return ToStringDict(val, comparer);
    }

    /// <summary>
    /// 任意オブジェクトを <see cref="string"/> キーの辞書へ正規化する。
    /// </summary>
    /// <param name="val">YAML/JSON 由来のオブジェクト。</param>
    /// <param name="comparer">キー比較子。省略時は序数無視比較。</param>
    /// <returns>正規化した辞書。</returns>
    protected static Dictionary<string, object?> ToStringDict(object? val, IEqualityComparer<string>? comparer = null)
    {
        var cmp = comparer ?? StringComparer.Ordinal;
        var result = new Dictionary<string, object?>(cmp);
        if (val == null)
        {
            return result;
        }

        if (val is Dictionary<string, object?> strDict)
        {
            foreach (var (key, value) in strDict)
            {
                result[key] = value;
            }

            return result;
        }

        if (val is IDictionary dict)
        {
            foreach (DictionaryEntry kv in dict)
            {
                result[kv.Key?.ToString() ?? ""] = kv.Value;
            }
        }

        return result;
    }

    /// <summary>
    /// 辞書の値を文字列として取得する。欠損・null のときは null。
    /// </summary>
    /// <param name="dict">キーと値の辞書。</param>
    /// <param name="key">読み取るキー。</param>
    /// <returns>文字列化した値、または null。</returns>
    protected static string? GetStr(Dictionary<string, object?> dict, string key)
    {
        ArgumentNullException.ThrowIfNull(dict);
        return dict.TryGetValue(key, out var v) && v != null ? v.ToString() : null;
    }

    /// <summary>
    /// 辞書の値を真偽として解釈する。欠損・null・非対応型のときは false。
    /// </summary>
    /// <param name="dict">キーと値の辞書。</param>
    /// <param name="key">読み取るキー。</param>
    /// <returns>真偽値。</returns>
    protected static bool GetBool(Dictionary<string, object?> dict, string key)
    {
        ArgumentNullException.ThrowIfNull(dict);
        if (!dict.TryGetValue(key, out var v) || v == null)
        {
            return false;
        }

        if (v is bool b)
        {
            return b;
        }

        return string.Equals(v.ToString(), "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 辞書の値を <see cref="int"/> に変換する。未設定・非対応型・パース不能のときは null。
    /// </summary>
    /// <param name="dict">キーと値の辞書。</param>
    /// <param name="key">読み取るキー。</param>
    /// <returns>変換できた整数、または null。</returns>
    protected static int? GetNullableInt(Dictionary<string, object?> dict, string key)
    {
        ArgumentNullException.ThrowIfNull(dict);
        if (!dict.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            int i => i,
            long l => checked((int)l),
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }

    /// <summary>
    /// 辞書の値を文字列の列として取得する。配列・列挙でない、または欠損のときは null。
    /// </summary>
    /// <param name="dict">キーと値の辞書。</param>
    /// <param name="key">読み取るキー。</param>
    /// <returns>文字列の一覧、または null。</returns>
    protected static IReadOnlyList<string>? GetStrList(Dictionary<string, object?> dict, string key)
    {
        ArgumentNullException.ThrowIfNull(dict);
        if (!dict.TryGetValue(key, out var v) || v == null)
        {
            return null;
        }

        if (v is IEnumerable enumerable && v is not string)
        {
            return enumerable.Cast<object?>().Select(x => x?.ToString() ?? "").ToList();
        }

        return null;
    }

    /// <summary>
    /// 辞書にキーが存在するかを序数無視で判定する。
    /// </summary>
    /// <param name="dict">キーと値の辞書。</param>
    /// <param name="key">検索するキー。</param>
    /// <returns>存在するとき true。</returns>
    protected static bool HasKeyIgnoreCase(Dictionary<string, object?> dict, string key)
    {
        ArgumentNullException.ThrowIfNull(dict);
        return dict.Keys.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>文字列が簡易 JSONPath 式（<c>$</c> または <c>$.</c> 始まり）かどうかを判定する。</summary>
    /// <param name="s">判定対象の文字列。</param>
    /// <returns>パス式として扱うとき true。</returns>
    private static bool IsPathExpression(string s) =>
        s == "$" || s.StartsWith("$.", StringComparison.Ordinal);

    /// <summary><c>${...}</c> 形式のテンプレート文字列を拒否する（厳格仕様では未サポート）。</summary>
    /// <param name="ownerLabel">エラーメッセージ用のオーナー文脈。省略可。</param>
    /// <param name="value">検査対象の文字列。</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> が <c>${</c> で始まるとき。</exception>
    private static void RejectTemplate(string? ownerLabel, string value)
    {
        if (!value.StartsWith("${", StringComparison.Ordinal))
        {
            return;
        }

        throw new ArgumentException(Format(ownerLabel, "'${...}' input templates are not supported; use $. paths only."));
    }

    /// <summary>
    /// ローダー由来の例外メッセージにオーナー文脈（ノード id 等）を付与する。
    /// </summary>
    protected static string Format(string? ownerLabel, string message) =>
        string.IsNullOrWhiteSpace(ownerLabel) ? message : $"Node '{ownerLabel}': {message}";
}
