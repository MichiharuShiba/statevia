using System.Collections;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Statevia.Core.Api.Application.Definition;

/// <summary>ルート YAML の判別（U10: nodes 配列 vs states）。Engine は nodes を解釈しない。</summary>
public enum WorkflowDefinitionYamlFormatKind
{
    /// <summary>従来の states 形式（または nodes キーなし）。</summary>
    States,

    /// <summary>UI 向け nodes 配列形式。</summary>
    Nodes
}

/// <summary>POST /definitions 等に渡す定義本文が nodes か states かを判定する。</summary>
public static class WorkflowDefinitionYamlFormat
{
    /// <summary>
    /// ルートをパースし形式を返す。
    /// </summary>
    /// <exception cref="ArgumentException">nodes と states が併存、または nodes が配列でない等。</exception>
    public static WorkflowDefinitionYamlFormatKind Analyze(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (string.IsNullOrWhiteSpace(content))
        {
            return WorkflowDefinitionYamlFormatKind.States;
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        object? raw;
        try
        {
            raw = deserializer.Deserialize<object>(new StringReader(content));
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Invalid YAML/JSON for workflow definition.", ex);
        }

        var root = ToStringDict(raw);
        var hasNodesKey = root.TryGetValue("nodes", out var nodesVal) && nodesVal != null;
        var hasStatesKey = root.TryGetValue("states", out var statesVal) && statesVal != null;

        var nodesIsArray = nodesVal is IList;
        var statesIsDict = statesVal is IDictionary;

        if (hasNodesKey && nodesIsArray && hasStatesKey && statesIsDict)
        {
            throw new ArgumentException(
                "Workflow definition cannot contain both 'nodes' (array) and 'states' (object) at the root.");
        }

        if (hasNodesKey)
        {
            if (!nodesIsArray)
            {
                throw new ArgumentException("Root 'nodes' must be an array when present.");
            }

            if (nodesVal is IList list && list.Count == 0)
            {
                throw new ArgumentException("Root 'nodes' array cannot be empty.");
            }

            return WorkflowDefinitionYamlFormatKind.Nodes;
        }

        return WorkflowDefinitionYamlFormatKind.States;
    }

    private static Dictionary<string, object?> ToStringDict(object? val)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
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
}
