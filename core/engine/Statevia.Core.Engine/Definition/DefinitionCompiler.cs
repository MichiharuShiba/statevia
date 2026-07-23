using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Engine.Definition;

/// <summary>
/// YAML/JSON の WorkflowDefinition をコンパイルし、実行可能な CompiledWorkflowDefinition を生成します。
/// 遷移テーブル、Fork/Join テーブル、Wait テーブルなどを構築します。
/// </summary>
public sealed class DefinitionCompiler
{
    private readonly IStateExecutorFactory _executorFactory;

    /// <summary>
    /// 状態実行ファクトリを指定してコンパイラを構築する。
    /// </summary>
    /// <param name="executorFactory">コンパイル済み定義に埋め込む <see cref="IStateExecutorFactory"/>。</param>
    public DefinitionCompiler(IStateExecutorFactory executorFactory) => _executorFactory = executorFactory;

    /// <summary>ワークフロー定義をコンパイルし、実行可能なコンパイル済み定義を返します。</summary>
    /// <param name="definition">ワークフロー定義。</param>
    /// <param name="resolvedModules">compile 時に確定した module alias → 版ピン。</param>
    /// <param name="stateActionBindings">状態別 Action バインディング。</param>
    public CompiledWorkflowDefinition Compile(
        WorkflowDefinition definition,
        IReadOnlyDictionary<string, ResolvedModuleBinding>? resolvedModules = null,
        IReadOnlyDictionary<string, StateActionBinding>? stateActionBindings = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new CompiledWorkflowDefinition
        {
            Name = definition.Name,
            Transitions = BuildTransitionTable(definition),
            ConditionalTransitions = BuildConditionalTransitionTable(definition),
            ForkTable = BuildForkTable(definition),
            JoinTable = BuildJoinTable(definition),
            WaitTable = BuildWaitTable(definition),
            StateInputs = BuildStateInputTable(definition),
            StateOutputs = BuildStateOutputTable(definition),
            InitialState = DetermineInitialState(definition),
            StateExecutorFactory = _executorFactory,
            ResolvedModules = resolvedModules ?? EmptyResolvedModules,
            StateActionBindings = stateActionBindings ?? EmptyStateActionBindings,
        };
    }

    private static readonly IReadOnlyDictionary<string, ResolvedModuleBinding> EmptyResolvedModules =
        new Dictionary<string, ResolvedModuleBinding>(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, StateActionBinding> EmptyStateActionBindings =
        new Dictionary<string, StateActionBinding>(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, StateInputDefinition> BuildStateInputTable(WorkflowDefinition definition)
    {
        var result = new Dictionary<string, StateInputDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.Input != null)
            {
                result[stateName] = stateDef.Input;
            }
        }
        return result;
    }

    private static Dictionary<string, string> BuildStateOutputTable(WorkflowDefinition definition)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (!string.IsNullOrWhiteSpace(stateDef.Output))
            {
                result[stateName] = stateDef.Output;
            }
        }

        return result;
    }

    private static Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>> BuildTransitionTable(WorkflowDefinition definition)
    {
        var result = new Dictionary<string, Dictionary<string, TransitionTarget>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.On == null)
            {
                continue;
            }
            var stateTransitions = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase);
            foreach (var (fact, trans) in stateDef.On)
            {
                if (stateName.Equals(trans.Next, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var target = ToTransitionTarget(trans);
                if (target.Next != null || target.Fork != null || target.End)
                {
                    stateTransitions[fact] = target;
                }
            }
            if (stateTransitions.Count > 0)
            {
                result[stateName] = stateTransitions;
            }
        }
        return result.ToDictionary(k => k.Key, v => (IReadOnlyDictionary<string, TransitionTarget>)v.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, IReadOnlyDictionary<string, CompiledFactTransition>> BuildConditionalTransitionTable(WorkflowDefinition definition)
    {
        var result = new Dictionary<string, Dictionary<string, CompiledFactTransition>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.On is null)
            {
                continue;
            }

            var stateTransitions = new Dictionary<string, CompiledFactTransition>(StringComparer.OrdinalIgnoreCase);
            foreach (var (fact, transition) in stateDef.On)
            {
                stateTransitions[fact] = CompileFactTransition(transition);
            }

            if (stateTransitions.Count > 0)
            {
                result[stateName] = stateTransitions;
            }
        }

        return result.ToDictionary(
            keySelector: pair => pair.Key,
            elementSelector: pair => (IReadOnlyDictionary<string, CompiledFactTransition>)pair.Value,
            comparer: StringComparer.OrdinalIgnoreCase);
    }

    private static CompiledFactTransition CompileFactTransition(TransitionDefinition transition)
    {
        var linearTarget = ToTransitionTarget(transition);
        var defaultTarget = transition.Default is null ? null : ToTransitionTarget(transition.Default);
        var orderedCases = CompileCases(transition.Cases);

        return new CompiledFactTransition
        {
            LinearTarget = linearTarget is { Next: null, Fork: null, End: false } ? null : linearTarget,
            Cases = orderedCases,
            DefaultTarget = defaultTarget is { Next: null, Fork: null, End: false } ? null : defaultTarget
        };
    }

    private static List<CompiledTransitionCase> CompileCases(IReadOnlyList<TransitionCaseDefinition>? cases)
    {
        if (cases is null || cases.Count == 0)
        {
            return [];
        }

        return cases
            .Select((transitionCase, index) => new CompiledTransitionCase
            {
                Order = transitionCase.Order,
                DeclarationIndex = index,
                When = transitionCase.When,
                Target = ToTransitionTarget(transitionCase.Transition)
            })
            .OrderBy(transitionCase => transitionCase.Order.HasValue ? 0 : 1)
            .ThenBy(transitionCase => transitionCase.Order ?? int.MaxValue)
            .ThenBy(transitionCase => transitionCase.DeclarationIndex)
            .ToList();
    }

    private static TransitionTarget ToTransitionTarget(TransitionDefinition transition) =>
        new() { Next = transition.Next, Fork = transition.Fork, End = transition.End };

    private static Dictionary<string, IReadOnlyList<string>> BuildForkTable(WorkflowDefinition definition)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.On == null)
            {
                continue;
            }
            foreach (var (_, trans) in stateDef.On)
            {
                if (trans.Fork != null && trans.Fork.Count > 0)
                {
                    result[stateName] = trans.Fork;
                    break;
                }
            }
        }
        return result;
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildJoinTable(WorkflowDefinition definition)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.Join != null && stateDef.Join.All.Count > 0)
            {
                result[stateName] = stateDef.Join.All;
            }
        }
        return result;
    }

    private static Dictionary<string, string> BuildWaitTable(WorkflowDefinition definition)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.Wait != null)
            {
                result[stateName] = stateDef.Wait.Event;
            }
        }
        return result;
    }

    private static string DetermineInitialState(WorkflowDefinition definition)
    {
        var allReferenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, stateDef) in definition.States)
        {
            if (stateDef.On != null)
            {
                foreach (var (_, trans) in stateDef.On)
                {
                    CollectTransitionReferences(trans, allReferenced);
                }
            }
            if (stateDef.Join?.All != null)
            {
                foreach (var s in stateDef.Join.All)
                {
                    allReferenced.Add(s);
                }
            }
        }
        return definition.States.Keys.FirstOrDefault(s => !allReferenced.Contains(s))
            ?? definition.States.Keys.First()
            ?? throw new InvalidOperationException("No states in definition");
    }

    private static void CollectTransitionReferences(TransitionDefinition? transition, HashSet<string> allReferenced)
    {
        if (transition is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(transition.Next))
        {
            allReferenced.Add(transition.Next);
        }

        if (transition.Fork is { Count: > 0 })
        {
            foreach (var forkTarget in transition.Fork)
            {
                allReferenced.Add(forkTarget);
            }
        }

        if (transition.Cases is not null)
        {
            foreach (var transitionCase in transition.Cases)
            {
                CollectTransitionReferences(transitionCase.Transition, allReferenced);
            }
        }

        CollectTransitionReferences(transition.Default, allReferenced);
    }
}
