using System.Collections;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Statevia.Core.Definition;

/// <summary>
/// YAML/JSON 文字列からワークフロー定義を読み込み、WorkflowDefinition を生成します。
/// </summary>
public sealed class DefinitionLoader
{
    private readonly IDeserializer _deserializer;

    public DefinitionLoader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithNodeTypeResolver(new ScalarPreservingNodeTypeResolver())
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>YAML/JSON コンテンツをパースしてワークフロー定義を返します。</summary>
    public WorkflowDefinition Load(string content)
    {
        var raw = _deserializer.Deserialize<object>(new StringReader(content))
            ?? throw new InvalidOperationException("Invalid YAML/JSON");
        var root = ToStringDict(raw);

        var workflowDict = GetChildDict(root, "workflow");
        var workflow = ParseWorkflow(workflowDict);
        var states = ParseStates(GetChildDict(root, "states"));

        return new WorkflowDefinition
        {
            Workflow = workflow,
            States = states
        };
    }

    private static WorkflowMetadata ParseWorkflow(Dictionary<string, object?> dict)
    {
        return new WorkflowMetadata
        {
            Name = GetStr(dict, "name") ?? "Unnamed"
        };
    }

    private static IReadOnlyDictionary<string, StateDefinition> ParseStates(Dictionary<string, object?> dict)
    {
        var result = new Dictionary<string, StateDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in dict)
        {
            if (value != null)
            {
                var stateDict = ToStringDict(value);
                if (stateDict.Count > 0)
                {
                    result[key] = ParseState(stateDict);
                }
            }
        }
        return result;
    }

    private static StateDefinition ParseState(Dictionary<string, object?> dict)
    {
        IReadOnlyDictionary<string, TransitionDefinition>? on = null;
        WaitDefinition? wait = null;
        JoinDefinition? join = null;

        if (dict.TryGetValue("on", out var onVal) && onVal != null)
        {
            var onDict = ToStringDict(onVal);
            if (onDict.Count > 0)
            {
                on = ParseOn(onDict);
            }
        }
        if (dict.TryGetValue("wait", out var waitVal) && waitVal != null)
        {
            var waitDict = ToStringDict(waitVal);
            var ev = GetStr(waitDict, "event");
            if (ev != null)
            {
                wait = new WaitDefinition { Event = ev };
            }
        }
        if (dict.TryGetValue("join", out var joinVal) && joinVal != null)
        {
            var joinDict = ToStringDict(joinVal);
            var allOf = GetStrList(joinDict, "allOf");
            if (allOf != null)
            {
                join = new JoinDefinition { AllOf = allOf };
            }
        }

        return new StateDefinition { On = on, Wait = wait, Join = join };
    }

    private static IReadOnlyDictionary<string, TransitionDefinition> ParseOn(Dictionary<string, object?> dict)
    {
        var result = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in dict)
        {
            if (value != null)
            {
                var transDict = ToStringDict(value);
                if (transDict.Count > 0)
                {
                    result[key] = ParseTransition(transDict);
                }
            }
        }
        return result;
    }

    private static TransitionDefinition ParseTransition(Dictionary<string, object?> dict) => new()
    {
        Next = GetStr(dict, "next"),
        Fork = GetStrList(dict, "fork"),
        End = GetBool(dict, "end")
    };

    private static Dictionary<string, object?> GetChildDict(Dictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var val) || val == null)
        {
            return [];
        }
        return ToStringDict(val);
    }

    private static Dictionary<string, object?> ToStringDict(object? val)
    {
        var result = new Dictionary<string, object?>();
        if (val == null)
        {
            return result;
        }
        if (val is Dictionary<string, object?> strDict)
        {
            return strDict;
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

    private static string? GetStr(Dictionary<string, object?> dict, string key) =>
        dict.TryGetValue(key, out var v) && v != null ? v.ToString() : null;

    private static bool GetBool(Dictionary<string, object?> dict, string key)
    {
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

    private static IReadOnlyList<string>? GetStrList(Dictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v == null)
        {
            return null;
        }
        if (v is IEnumerable enumerable)
        {
            return enumerable.Cast<object?>().Select(x => x?.ToString() ?? "").ToList();
        }
        return null;
    }
}
