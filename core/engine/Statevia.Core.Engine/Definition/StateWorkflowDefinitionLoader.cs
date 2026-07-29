using System.Collections;

namespace Statevia.Core.Engine.Definition;

/// <summary>
/// YAML/JSON 文字列からワークフロー定義を読み込み、WorkflowDefinition を生成します。
/// states 形式（ルート <c>workflow</c> + <c>states</c>）専用。
/// </summary>
public sealed class StateWorkflowDefinitionLoader : WorkflowDefinitionLoaderBase
{
    /// <summary>
    /// スカラー保持用のノード型リゾルバを有効にしたローダーを構築する。
    /// </summary>
    public StateWorkflowDefinitionLoader()
        : base(useScalarPreservingNodeTypeResolver: true)
    {
    }

    /// <inheritdoc />
    protected override WorkflowDefinition BuildDefinition(Dictionary<string, object?> root)
    {
        var workflowDict = GetChildDict(root, "workflow");

        return new WorkflowDefinition
        {
            Name = GetStr(workflowDict, "name") ?? "Unnamed",
            Modules = ParseWorkflowModules(workflowDict),
            States = ParseStates(GetChildDict(root, "states")),
        };
    }

    private static Dictionary<string, StateDefinition> ParseStates(Dictionary<string, object?> dict)
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
            wait = ParseWaitDefinition(ToStringDict(waitVal), on);
        }
        if (dict.TryGetValue("join", out var joinVal) && joinVal != null)
        {
            var joinDict = ToStringDict(joinVal);

            var all = GetStrList(joinDict, "all");
            if (all == null)
            {
                throw new ArgumentException("join must specify a non-empty 'all' list of state names.");
            }

            join = new JoinDefinition { All = all };
        }

        if (dict.TryGetValue("input", out var inputVal) && inputVal != null)
        {
            stateInput = ParseStateInput(inputVal);
        }

        var action = GetStr(dict, "action");
        var output = GetStr(dict, "output");
        var retry = ParseRetryDefinition(dict);

        return new StateDefinition
        {
            Action = action,
            On = on,
            Wait = wait,
            Join = join,
            Input = stateInput,
            Output = output,
            Retry = retry,
        };
    }

    /// <summary>
    /// <c>wait</c> ブロックを <see cref="WaitDefinition"/> へ変換する。
    /// </summary>
    /// <param name="waitDict"><c>wait</c> 辞書。</param>
    /// <param name="on">同一状態の <c>on</c>（旧形式正規化で <c>Completed</c> 遷移先を参照）。</param>
    /// <returns>イベント表を持つ Wait 定義。空の wait は null。</returns>
    /// <exception cref="ArgumentException"><c>exits</c> 指定、または <c>events</c> と <c>event</c> の併用。</exception>
    private static WaitDefinition? ParseWaitDefinition(
        Dictionary<string, object?> waitDict,
        IReadOnlyDictionary<string, TransitionDefinition>? on)
    {
        if (HasKeyIgnoreCase(waitDict, "exits"))
        {
            throw new ArgumentException("wait.exits is not supported; use wait.events.");
        }

        var hasEvents = HasKeyIgnoreCase(waitDict, "events");
        var legacyEvent = GetStr(waitDict, "event");
        if (hasEvents && !string.IsNullOrWhiteSpace(legacyEvent))
        {
            throw new ArgumentException("wait.events and wait.event cannot be used together.");
        }

        var assignees = GetStrList(waitDict, "assignees");

        if (hasEvents)
        {
            var eventsKey = waitDict.Keys.First(k => string.Equals(k, "events", StringComparison.OrdinalIgnoreCase));
            var events = ParseWaitEventsMap(waitDict[eventsKey]);
            if (events.Count == 0 && (assignees == null || assignees.Count == 0))
            {
                return null;
            }

            return new WaitDefinition { Events = events, Assignees = assignees };
        }

        if (string.IsNullOrWhiteSpace(legacyEvent))
        {
            if (assignees is { Count: > 0 })
            {
                return new WaitDefinition
                {
                    Events = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    Assignees = assignees
                };
            }

            return null;
        }

        // 旧形式: wait.event + on.Completed.next → events
        var next = string.Empty;
        if (on != null && on.TryGetValue("Completed", out var completed))
        {
            next = completed.Next ?? string.Empty;
        }

        return new WaitDefinition
        {
            Events = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [legacyEvent.Trim()] = next
            },
            Assignees = assignees
        };
    }

    /// <summary>
    /// <c>wait.events</c> マップ（イベント名 → 遷移先）を読み取る。
    /// </summary>
    /// <remarks>
    /// 遷移先が空／空白のみのエントリは不正とし、読み込み時に例外を投げる（FSM フォールバックはしない）。
    /// </remarks>
    private static Dictionary<string, string> ParseWaitEventsMap(object? eventsVal)
    {
        if (eventsVal == null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var eventsDict = ToStringDict(eventsVal);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (eventName, rawNext) in eventsDict)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException("wait.events keys must be non-empty event names.");
            }

            var next = rawNext?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(next))
            {
                throw new ArgumentException($"wait.events['{eventName}'] must specify a next state name.");
            }

            if (!result.TryAdd(eventName.Trim(), next))
            {
                throw new ArgumentException($"wait.events contains duplicate event name '{eventName}'.");
            }
        }

        return result;
    }

    private static Dictionary<string, TransitionDefinition> ParseOn(Dictionary<string, object?> dict)
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
        End = GetBool(dict, "end"),
        Cases = ParseCases(dict),
        Default = ParseDefaultTransition(dict)
    };

    private static List<TransitionCaseDefinition>? ParseCases(Dictionary<string, object?> dict)
    {
        if (!dict.TryGetValue("cases", out var casesVal) || casesVal is null)
        {
            return null;
        }

        if (casesVal is not IEnumerable enumerable || casesVal is string)
        {
            return null;
        }

        var result = new List<TransitionCaseDefinition>();
        foreach (var rawCase in enumerable)
        {
            if (rawCase is null)
            {
                continue;
            }

            var caseDict = ToStringDict(rawCase);
            if (caseDict.Count == 0)
            {
                continue;
            }

            result.Add(ParseCase(caseDict));
        }

        return result.Count == 0 ? null : result;
    }

    private static TransitionCaseDefinition ParseCase(Dictionary<string, object?> caseDict)
    {
        if (!caseDict.TryGetValue("when", out var whenVal) || whenVal is null)
        {
            throw new ArgumentException("case requires 'when'.");
        }

        var whenDict = ToStringDict(whenVal);
        var when = ParseConditionWhen(whenDict);

        var transition = ParseTransition(caseDict);

        var order = GetNullableInt(caseDict, "order");
        return new TransitionCaseDefinition
        {
            Order = order,
            When = when,
            Transition = transition
        };
    }

    private static TransitionDefinition? ParseDefaultTransition(Dictionary<string, object?> dict)
    {
        if (!dict.TryGetValue("default", out var defaultVal) || defaultVal is null)
        {
            return null;
        }

        if (defaultVal is string defaultNextState)
        {
            return new TransitionDefinition { Next = defaultNextState };
        }

        var defaultDict = ToStringDict(defaultVal);
        if (defaultDict.Count == 0)
        {
            return null;
        }

        return ParseTransition(defaultDict);
    }

    private static StateInputDefinition ParseStateInput(object inputVal)
        => ParseStrictInputMapping(inputVal)
            ?? throw new ArgumentException("input mapping is required.");

}
