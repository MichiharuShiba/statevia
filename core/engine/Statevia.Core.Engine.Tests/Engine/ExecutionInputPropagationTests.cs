using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Statevia.Core.Engine.ExecutionGraphs;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

/// <summary>
/// workflow-input-output-spec フェーズ A: Start の <c>input</c> 引数・next 伝播・Fork Broadcast・Join 辞書。
/// </summary>
public class ExecutionInputPropagationTests
{
    /// <summary>Start に渡した input が初期状態の Execute にそのまま渡ることを検証する。</summary>
    [Fact]
    public async Task Start_passes_input_to_initial_state()
    {
        // Arrange
        object? seen = "unset";
        var def = CreateSingleStateDefinition(
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                seen = input;
                return Task.FromResult<object?>("done");
            })));
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var id = engine.Start(def, null, "workflow-seed");

        // Act
        await WaitUntilCompletedAsync(engine, id).ConfigureAwait(false);

        // Assert
        Assert.Equal("workflow-seed", seen);
    }

    /// <summary>実行グラフ JSON の初期状態ノードに Start input が記録されることを検証する。</summary>
    [Fact]
    public async Task ExportExecutionGraph_records_input_on_initial_state_node()
    {
        var def = CreateSingleStateDefinition(
            DefaultStateExecutor.Create(new ImmediateState()));
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var seed = new Dictionary<string, object?> { ["k"] = "v" };
        var id = engine.Start(def, null, seed);

        await WaitUntilCompletedAsync(engine, id).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(engine.ExportExecutionGraph(id));
        var nodes = doc.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        var startNode = Assert.Single(nodes, n => n.GetProperty("stateName").GetString() == "Start");
        Assert.Equal(JsonValueKind.Object, startNode.GetProperty("input").ValueKind);
        Assert.Equal("v", startNode.GetProperty("input").GetProperty("k").GetString());
    }

    /// <summary>next 遷移で後続状態には直前状態の出力が input として渡ることを検証する。</summary>
    [Fact]
    public async Task Next_transition_passes_previous_output()
    {
        // Arrange
        object? bInput = "unset";
        var def = CreateTwoStateChain(
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                _ = input;
                return Task.FromResult<object?>("from-A");
            })),
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                bInput = input;
                return Task.FromResult<object?>(null);
            })));
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var id = engine.Start(def);

        // Act
        await WaitUntilCompletedAsync(engine, id).ConfigureAwait(false);

        // Assert
        Assert.Equal("from-A", bInput);
    }

    [Fact]
    public async Task Fork_broadcasts_same_output_to_each_branch()
    {
        var inputs = new ConcurrentBag<object?>();
        var def = CreateForkDefinition(
            DefaultStateExecutor.Create(new DelegateState((_, _, _) =>
                Task.FromResult<object?>("fork-source"))),
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                inputs.Add(input);
                return Task.FromResult<object?>("a");
            })),
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                inputs.Add(input);
                return Task.FromResult<object?>("b");
            })));
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var id = engine.Start(def);

        await WaitUntilCompletedAsync(engine, id).ConfigureAwait(false);

        Assert.Equal(2, inputs.Count);
        Assert.All(inputs, x => Assert.Equal("fork-source", x));
    }

    /// <summary>Join 完了後の次状態には、分岐状態名 → 出力の辞書が input として渡ることを検証する。</summary>
    [Fact]
    public async Task Join_passes_join_dictionary_to_next_state()
    {
        // Arrange
        object? afterInput = "unset";
        var def = CreateJoinThenNextDefinition(
            DefaultStateExecutor.Create(new DelegateState((_, _, _) => Task.FromResult<object?>("out-A"))),
            DefaultStateExecutor.Create(new DelegateState((_, _, _) => Task.FromResult<object?>("out-B"))),
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                afterInput = input;
                return Task.FromResult<object?>(null);
            })));
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var id = engine.Start(def);

        // Act
        await WaitUntilCompletedAsync(engine, id).ConfigureAwait(false);

        // Assert
        var dict = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(afterInput);
        Assert.NotNull(dict);
        Assert.Equal("out-A", dict["A"]);
        Assert.Equal("out-B", dict["B"]);
    }

    /// <summary>
    /// 実行グラフに Join 状態のノードが 1 つだけ存在し、A/B 分岐の Join 辺が同一 Join ノード ID に収束することを検証する。
    /// </summary>
    [Fact]
    public async Task Join_execution_graph_has_single_join_node_and_join_edge_targets_that_node()
    {
        var def = CreateJoinThenNextDefinition(
            DefaultStateExecutor.Create(new DelegateState((_, _, _) => Task.FromResult<object?>("out-A"))),
            DefaultStateExecutor.Create(new DelegateState((_, _, _) => Task.FromResult<object?>("out-B"))),
            DefaultStateExecutor.Create(new DelegateState((_, _, _) => Task.FromResult<object?>(null))));
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var id = engine.Start(def);

        await WaitUntilCompletedAsync(engine, id).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(engine.ExportExecutionGraph(id));
        var root = doc.RootElement;
        var nodes = root.GetProperty("nodes").EnumerateArray().ToList();
        var joinNodes = nodes.Where(n => n.GetProperty("stateName").GetString() == "Join1").ToList();
        Assert.Single(joinNodes);
        var joinNodeId = joinNodes[0].GetProperty("nodeId").GetString();
        Assert.NotNull(joinNodeId);

        var edges = root.GetProperty("edges").EnumerateArray().ToList();
        var joinEdges = edges.Where(e => e.GetProperty("type").GetInt32() == (int)EdgeType.Join).ToList();
        Assert.Equal(2, joinEdges.Count);
        Assert.All(joinEdges, e => Assert.Equal(joinNodeId, e.GetProperty("to").GetString()));

        var expectedJoinSources = nodes
            .Where(n =>
                n.GetProperty("stateName").GetString() == "A"
                || n.GetProperty("stateName").GetString() == "B")
            .Select(n => n.GetProperty("nodeId").GetString())
            .ToHashSet();
        var actualJoinSources = joinEdges.Select(e => e.GetProperty("from").GetString()).ToHashSet();
        Assert.Equal(expectedJoinSources.Count, actualJoinSources.Count);
        Assert.All(expectedJoinSources, sourceNodeId => Assert.Contains(sourceNodeId, actualJoinSources));

        var fromJoinNext = edges.Where(e =>
            e.GetProperty("type").GetInt32() == (int)EdgeType.Next
            && e.GetProperty("from").GetString() == joinNodeId).ToList();
        Assert.Single(fromJoinNext);
        Assert.Equal("AfterJoin", nodes.Single(n => n.GetProperty("nodeId").GetString() == fromJoinNext[0].GetProperty("to").GetString()).GetProperty("stateName").GetString());

        var joinInput = joinNodes[0].GetProperty("input");
        var joinInputKeys = joinInput.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("A", joinInputKeys);
        Assert.Contains("B", joinInputKeys);

        Assert.Equal(1, joinNodes[0].GetProperty("attempt").GetInt32());
        var joinWorkerId = joinNodes[0].GetProperty("workerId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(joinWorkerId));
        Assert.False(joinNodes[0].GetProperty("canceledByExecution").GetBoolean());
    }

    /// <summary>next 遷移先の input.path が raw input に適用されることを検証する。</summary>
    [Fact]
    public async Task Next_transition_applies_state_input_path()
    {
        // Arrange
        object? bInput = "unset";
        var def = CreateTwoStateChainWithStateInput(
            DefaultStateExecutor.Create(new DelegateState((_, _, _) =>
                Task.FromResult<object?>(new Dictionary<string, object?> { ["payload"] = "mapped-value" }))),
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                bInput = input;
                return Task.FromResult<object?>(null);
            })),
            new StateInputDefinition { Path = "$.payload" });
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var id = engine.Start(def);

        // Act
        await WaitUntilCompletedAsync(engine, id).ConfigureAwait(false);

        // Assert
        Assert.Equal("mapped-value", bInput);
    }

    /// <summary>Fork 分岐先ごとに input.path が適用されることを検証する。</summary>
    [Fact]
    public async Task Fork_transition_applies_state_input_per_branch()
    {
        // Arrange
        var aInput = "unset";
        var bInput = "unset";
        var def = CreateForkDefinitionWithStateInput(
            DefaultStateExecutor.Create(new DelegateState((_, _, _) =>
                Task.FromResult<object?>(new Dictionary<string, object?> { ["v"] = "fork-mapped" }))),
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                aInput = input?.ToString() ?? "null";
                return Task.FromResult<object?>("a");
            })),
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                bInput = input?.ToString() ?? "null";
                return Task.FromResult<object?>("b");
            })),
            new Dictionary<string, StateInputDefinition>
            {
                ["A"] = new() { Path = "$.v" },
                ["B"] = new() { Path = "$.v" }
            });
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var id = engine.Start(def);

        // Act
        await WaitUntilCompletedAsync(engine, id).ConfigureAwait(false);

        // Assert
        Assert.Equal("fork-mapped", aInput);
        Assert.Equal("fork-mapped", bInput);
    }

    /// <summary>Join 後の next 遷移先に input.path が適用されることを検証する。</summary>
    [Fact]
    public async Task Join_next_applies_state_input_path()
    {
        // Arrange
        object? afterInput = "unset";
        var def = CreateJoinThenNextWithStateInput(
            DefaultStateExecutor.Create(new DelegateState((_, _, _) => Task.FromResult<object?>("out-A"))),
            DefaultStateExecutor.Create(new DelegateState((_, _, _) => Task.FromResult<object?>("out-B"))),
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                afterInput = input;
                return Task.FromResult<object?>(null);
            })),
            new StateInputDefinition { Path = "$.A" });
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var id = engine.Start(def);

        // Act
        await WaitUntilCompletedAsync(engine, id).ConfigureAwait(false);

        // Assert
        Assert.Equal("out-A", afterInput);
    }

    /// <summary>input マップで複数キー・ネストキー・リテラルが構築されることを検証する。</summary>
    [Fact]
    public async Task Next_transition_builds_object_with_nested_and_literal_values()
    {
        // Arrange
        object? bInput = null;
        var def = CreateTwoStateChainWithStateInput(
            DefaultStateExecutor.Create(new DelegateState((_, _, _) =>
                Task.FromResult<object?>(new Dictionary<string, object?> { ["a"] = new Dictionary<string, object?> { ["b"] = "from-path" } }))),
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                bInput = input;
                return Task.FromResult<object?>(null);
            })),
            new StateInputDefinition
            {
                Values = new Dictionary<string, StateInputValueDefinition>
                {
                    ["foo"] = new() { Path = "$.a" },
                    ["foo.bar"] = new() { Path = "$.a.b" },
                    ["title"] = new() { Literal = "my song" },
                    ["count"] = new() { Literal = 2L },
                    ["enabled"] = new() { Literal = true }
                }
            });
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var id = engine.Start(def);

        // Act
        await WaitUntilCompletedAsync(engine, id).ConfigureAwait(false);

        // Assert
        var dict = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(bInput);
        Assert.Equal("my song", dict["title"]);
        Assert.Equal(2L, dict["count"]);
        Assert.True((bool)dict["enabled"]!);
        var foo = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(dict["foo"]);
        Assert.Equal("from-path", foo["bar"]);
    }

    private static async Task WaitUntilCompletedAsync(ExecutionEngine engine, string executionId, int maxMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxMs);
        while (DateTime.UtcNow < deadline)
        {
            var s = engine.GetSnapshot(executionId);
            if (s is { IsCompleted: true })
            {
                return;
            }

            await Task.Delay(20).ConfigureAwait(false);
        }

        Assert.Fail("Execution did not complete in time.");
    }

    private sealed class DelegateState : IState<object?, object?>
    {
        private readonly Func<StateContext, object?, CancellationToken, Task<object?>> _fn;
        public DelegateState(Func<StateContext, object?, CancellationToken, Task<object?>> fn) => _fn = fn;
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) => _fn(ctx, input, ct);
    }

    private static CompiledWorkflowDefinition CreateSingleStateDefinition(IStateExecutor start)
    {
        return new CompiledWorkflowDefinition
        {
            Name = "Single",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["Start"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
            },
            ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
            JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
            WaitTable = new Dictionary<string, string>(),
            InitialState = "Start",
            StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor> { ["Start"] = start })
        };
    }

    private static CompiledWorkflowDefinition CreateTwoStateChain(IStateExecutor a, IStateExecutor b)
    {
        return new CompiledWorkflowDefinition
        {
            Name = "Chain",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["A"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "B" } },
                ["B"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
            },
            ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
            JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
            WaitTable = new Dictionary<string, string>(),
            InitialState = "A",
            StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor> { ["A"] = a, ["B"] = b })
        };
    }

    private static CompiledWorkflowDefinition CreateTwoStateChainWithStateInput(
        IStateExecutor a,
        IStateExecutor b,
        StateInputDefinition mappingForB)
    {
        return new CompiledWorkflowDefinition
        {
            Name = "ChainMapped",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["A"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "B" } },
                ["B"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
            },
            ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
            JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
            WaitTable = new Dictionary<string, string>(),
            StateInputs = new Dictionary<string, StateInputDefinition> { ["B"] = mappingForB },
            InitialState = "A",
            StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor> { ["A"] = a, ["B"] = b })
        };
    }

    private static CompiledWorkflowDefinition CreateForkDefinition(IStateExecutor start, IStateExecutor a, IStateExecutor b)
    {
        return new CompiledWorkflowDefinition
        {
            Name = "Fork",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["Start"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Fork = new[] { "A", "B" } } },
                ["A"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } },
                ["B"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
            },
            ForkTable = new Dictionary<string, IReadOnlyList<string>> { ["Start"] = new[] { "A", "B" } },
            JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
            WaitTable = new Dictionary<string, string>(),
            InitialState = "Start",
            StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
            {
                ["Start"] = start,
                ["A"] = a,
                ["B"] = b
            })
        };
    }

    private static CompiledWorkflowDefinition CreateForkDefinitionWithStateInput(
        IStateExecutor start,
        IStateExecutor a,
        IStateExecutor b,
        IReadOnlyDictionary<string, StateInputDefinition> stateInputs)
    {
        return new CompiledWorkflowDefinition
        {
            Name = "ForkMapped",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["Start"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Fork = new[] { "A", "B" } } },
                ["A"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } },
                ["B"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
            },
            ForkTable = new Dictionary<string, IReadOnlyList<string>> { ["Start"] = new[] { "A", "B" } },
            JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
            WaitTable = new Dictionary<string, string>(),
            StateInputs = stateInputs,
            InitialState = "Start",
            StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
            {
                ["Start"] = start,
                ["A"] = a,
                ["B"] = b
            })
        };
    }

    /// <summary>
    /// A/B には <c>Completed</c> 遷移を置かない（Join へは <see cref="JoinTracker.RecordFact"/> のみ）。
    /// これにより Join1 は all 完了後にのみ <see cref="ExecutionEngine"/> から起動される。
    /// </summary>
    private static CompiledWorkflowDefinition CreateJoinThenNextDefinition(
        IStateExecutor a,
        IStateExecutor b,
        IStateExecutor afterJoin)
    {
        return new CompiledWorkflowDefinition
        {
            Name = "JoinNext",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["Start"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Fork = new[] { "A", "B" } } },
                ["Join1"] = new Dictionary<string, TransitionTarget> { ["Joined"] = new TransitionTarget { Next = "AfterJoin" } },
                ["AfterJoin"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
            },
            ForkTable = new Dictionary<string, IReadOnlyList<string>> { ["Start"] = new[] { "A", "B" } },
            JoinTable = new Dictionary<string, IReadOnlyList<string>> { ["Join1"] = new[] { "A", "B" } },
            WaitTable = new Dictionary<string, string>(),
            InitialState = "Start",
            StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
            {
                ["Start"] = DefaultStateExecutor.Create(new ImmediateState()),
                ["A"] = a,
                ["B"] = b,
                ["AfterJoin"] = afterJoin
            })
        };
    }

    private static CompiledWorkflowDefinition CreateJoinThenNextWithStateInput(
        IStateExecutor a,
        IStateExecutor b,
        IStateExecutor afterJoin,
        StateInputDefinition mappingForAfterJoin)
    {
        return new CompiledWorkflowDefinition
        {
            Name = "JoinNextMapped",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["Start"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Fork = new[] { "A", "B" } } },
                ["Join1"] = new Dictionary<string, TransitionTarget> { ["Joined"] = new TransitionTarget { Next = "AfterJoin" } },
                ["AfterJoin"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
            },
            ForkTable = new Dictionary<string, IReadOnlyList<string>> { ["Start"] = new[] { "A", "B" } },
            JoinTable = new Dictionary<string, IReadOnlyList<string>> { ["Join1"] = new[] { "A", "B" } },
            WaitTable = new Dictionary<string, string>(),
            StateInputs = new Dictionary<string, StateInputDefinition> { ["AfterJoin"] = mappingForAfterJoin },
            InitialState = "Start",
            StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
            {
                ["Start"] = DefaultStateExecutor.Create(new ImmediateState()),
                ["A"] = a,
                ["B"] = b,
                ["AfterJoin"] = afterJoin
            })
        };
    }

    private sealed class ImmediateState : IState<Unit, Unit>
    {
        public Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct) => Task.FromResult(Unit.Value);
    }
}
