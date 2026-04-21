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

        var map = ToStringDict(inputVal, StringComparer.OrdinalIgnoreCase);
        if (map.Count == 1
            && map.TryGetValue("path", out var pathVal)
            && pathVal is string onlyPath)
        {
            RejectTemplate(ownerLabel, onlyPath);
            if (IsPathExpression(onlyPath))
            {
                if (!SimpleJsonPath.IsValid(onlyPath))
                {
                    throw new ArgumentException(Format(ownerLabel, $"invalid input.path: '{onlyPath}'."));
                }

                return new StateInputDefinition { Path = onlyPath };
            }
        }

        var values = new Dictionary<string, StateInputValueDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, raw) in map)
        {
            if (raw is string str)
            {
                RejectTemplate(ownerLabel, str);
                if (IsPathExpression(str))
                {
                    if (!SimpleJsonPath.IsValid(str))
                    {
                        throw new ArgumentException(Format(ownerLabel, $"invalid input path for key '{key}': '{str}'."));
                    }

                    values[key] = new StateInputValueDefinition { Path = str };
                }
                else
                {
                    values[key] = new StateInputValueDefinition { Literal = str };
                }
            }
            else
            {
                values[key] = new StateInputValueDefinition { Literal = raw };
            }
        }

        return new StateInputDefinition { Values = values };
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

    protected static string? GetStr(Dictionary<string, object?> dict, string key)
    {
        ArgumentNullException.ThrowIfNull(dict);
        return dict.TryGetValue(key, out var v) && v != null ? v.ToString() : null;
    }

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

    protected static bool HasKeyIgnoreCase(Dictionary<string, object?> dict, string key)
    {
        ArgumentNullException.ThrowIfNull(dict);
        return dict.Keys.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsPathExpression(string s) =>
        s == "$" || s.StartsWith("$.", StringComparison.Ordinal);

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
