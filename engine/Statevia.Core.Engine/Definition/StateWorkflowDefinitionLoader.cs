namespace Statevia.Core.Engine.Definition;

/// <summary>
/// YAML/JSON 文字列からワークフロー定義を読み込み、WorkflowDefinition を生成します。
/// states 形式（ルート <c>workflow</c> + <c>states</c>）専用。
/// </summary>
public sealed class StateWorkflowDefinitionLoader : WorkflowDefinitionLoaderBase
{
    public StateWorkflowDefinitionLoader()
        : base(useScalarPreservingNodeTypeResolver: true)
    {
    }

    /// <inheritdoc />
    protected override WorkflowDefinition BuildDefinition(Dictionary<string, object?> root)
    {
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
        StateInputDefinition? stateInput = null;

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

        if (dict.TryGetValue("input", out var inputVal) && inputVal != null)
        {
            stateInput = ParseStateInput(inputVal);
        }

        var action = GetStr(dict, "action");

        return new StateDefinition { Action = action, On = on, Wait = wait, Join = join, Input = stateInput };
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

    private static StateInputDefinition ParseStateInput(object inputVal)
        => ParseStrictInputMapping(inputVal)
            ?? throw new ArgumentException("input mapping is required.");

    private static void EnsureOnlyKnownKeys(Dictionary<string, object?> dict, IReadOnlyCollection<string> knownKeys, string sectionName)
    {
        foreach (var key in dict.Keys)
        {
            if (!knownKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unknown key in {sectionName}: {key}");
            }
        }
    }
}
