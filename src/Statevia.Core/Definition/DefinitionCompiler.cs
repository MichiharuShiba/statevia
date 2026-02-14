using Statevia.Core.Abstractions;

namespace Statevia.Core.Definition;

/// <summary>
/// YAML/JSON の WorkflowDefinition をコンパイルし、実行可能な CompiledWorkflowDefinition を生成します。
/// 遷移テーブル、Fork/Join テーブル、Wait テーブルなどを構築します。
/// </summary>
public sealed class DefinitionCompiler
{
    private readonly IStateExecutorFactory _executorFactory;

    public DefinitionCompiler(IStateExecutorFactory executorFactory) => _executorFactory = executorFactory;

    /// <summary>ワークフロー定義をコンパイルし、実行可能なコンパイル済み定義を返します。</summary>
    public CompiledWorkflowDefinition Compile(WorkflowDefinition definition)
    {
        return new CompiledWorkflowDefinition
        {
            Name = definition.Workflow.Name,
            Transitions = BuildTransitionTable(definition),
            ForkTable = BuildForkTable(definition),
            JoinTable = BuildJoinTable(definition),
            WaitTable = BuildWaitTable(definition),
            InitialState = DetermineInitialState(definition),
            StateExecutorFactory = _executorFactory
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, TransitionTarget>> BuildTransitionTable(WorkflowDefinition definition)
    {
        var result = new Dictionary<string, Dictionary<string, TransitionTarget>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.On == null) continue;
            var stateTransitions = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase);
            foreach (var (fact, trans) in stateDef.On)
            {
                if (stateName.Equals(trans.Next, StringComparison.OrdinalIgnoreCase)) continue;
                var target = new TransitionTarget { Next = trans.Next, Fork = trans.Fork, End = trans.End };
                if (target.Next != null || target.Fork != null || target.End)
                    stateTransitions[fact] = target;
            }
            if (stateTransitions.Count > 0) result[stateName] = stateTransitions;
        }
        return result.ToDictionary(k => k.Key, v => (IReadOnlyDictionary<string, TransitionTarget>)v.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildForkTable(WorkflowDefinition definition)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.On == null) continue;
            foreach (var (_, trans) in stateDef.On)
            {
                if (trans.Fork != null && trans.Fork.Count > 0) { result[stateName] = trans.Fork; break; }
            }
        }
        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildJoinTable(WorkflowDefinition definition)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.Join != null && stateDef.Join.AllOf.Count > 0)
                result[stateName] = stateDef.Join.AllOf;
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string> BuildWaitTable(WorkflowDefinition definition)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.Wait != null) result[stateName] = stateDef.Wait.Event;
        }
        return result;
    }

    private static string DetermineInitialState(WorkflowDefinition definition)
    {
        var allReferenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, stateDef) in definition.States)
        {
            if (stateDef.On != null)
                foreach (var (_, trans) in stateDef.On)
                {
                    if (trans.Next != null) allReferenced.Add(trans.Next);
                    if (trans.Fork != null) foreach (var s in trans.Fork) allReferenced.Add(s);
                }
            if (stateDef.Join?.AllOf != null)
                foreach (var s in stateDef.Join.AllOf) allReferenced.Add(s);
        }
        return definition.States.Keys.FirstOrDefault(s => !allReferenced.Contains(s))
            ?? definition.States.Keys.First()
            ?? throw new InvalidOperationException("No states in definition");
    }
}
