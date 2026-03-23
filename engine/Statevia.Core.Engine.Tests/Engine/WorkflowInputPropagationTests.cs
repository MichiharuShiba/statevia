using System.Collections.Concurrent;
using System.Collections.Generic;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

/// <summary>
/// workflow-input-output-spec フェーズ A: 初期 input・next 伝播・Fork Broadcast・Join 辞書。
/// </summary>
public class WorkflowInputPropagationTests
{
    /// <summary>Start に渡した workflowInput が初期状態の Execute にそのまま渡ることを検証する。</summary>
    [Fact]
    public async Task Start_passes_workflowInput_to_initial_state()
    {
        // Arrange
        object? seen = "unset";
        var def = CreateSingleStateDefinition(
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                seen = input;
                return Task.FromResult<object?>("done");
            })));
        using var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var id = engine.Start(def, null, "workflow-seed");

        // Act
        await WaitUntilCompletedAsync(engine, id).ConfigureAwait(false);

        // Assert
        Assert.Equal("workflow-seed", seen);
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
        using var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
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
        using var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
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
        using var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var id = engine.Start(def);

        // Act
        await WaitUntilCompletedAsync(engine, id).ConfigureAwait(false);

        // Assert
        var dict = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(afterInput);
        Assert.NotNull(dict);
        Assert.Equal("out-A", dict["A"]);
        Assert.Equal("out-B", dict["B"]);
    }

    /// <summary>next 遷移先の inputMapping.path が raw input に適用されることを検証する。</summary>
    [Fact]
    public async Task Next_transition_applies_inputMapping_path()
    {
        // Arrange
        object? bInput = "unset";
        var def = CreateTwoStateChainWithInputMapping(
            DefaultStateExecutor.Create(new DelegateState((_, _, _) =>
                Task.FromResult<object?>(new Dictionary<string, object?> { ["payload"] = "mapped-value" }))),
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                bInput = input;
                return Task.FromResult<object?>(null);
            })),
            new InputMappingDefinition { Path = "$.payload" });
        using var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var id = engine.Start(def);

        // Act
        await WaitUntilCompletedAsync(engine, id).ConfigureAwait(false);

        // Assert
        Assert.Equal("mapped-value", bInput);
    }

    /// <summary>Fork 分岐先ごとに inputMapping.path が適用されることを検証する。</summary>
    [Fact]
    public async Task Fork_transition_applies_inputMapping_per_branch()
    {
        // Arrange
        var aInput = "unset";
        var bInput = "unset";
        var def = CreateForkDefinitionWithInputMapping(
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
            new Dictionary<string, InputMappingDefinition>
            {
                ["A"] = new() { Path = "$.v" },
                ["B"] = new() { Path = "$.v" }
            });
        using var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var id = engine.Start(def);

        // Act
        await WaitUntilCompletedAsync(engine, id).ConfigureAwait(false);

        // Assert
        Assert.Equal("fork-mapped", aInput);
        Assert.Equal("fork-mapped", bInput);
    }

    /// <summary>Join 後の next 遷移先に inputMapping.path が適用されることを検証する。</summary>
    [Fact]
    public async Task Join_next_applies_inputMapping_path()
    {
        // Arrange
        object? afterInput = "unset";
        var def = CreateJoinThenNextWithInputMapping(
            DefaultStateExecutor.Create(new DelegateState((_, _, _) => Task.FromResult<object?>("out-A"))),
            DefaultStateExecutor.Create(new DelegateState((_, _, _) => Task.FromResult<object?>("out-B"))),
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                afterInput = input;
                return Task.FromResult<object?>(null);
            })),
            new InputMappingDefinition { Path = "$.A" });
        using var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var id = engine.Start(def);

        // Act
        await WaitUntilCompletedAsync(engine, id).ConfigureAwait(false);

        // Assert
        Assert.Equal("out-A", afterInput);
    }

    private static async Task WaitUntilCompletedAsync(WorkflowEngine engine, string workflowId, int maxMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxMs);
        while (DateTime.UtcNow < deadline)
        {
            var s = engine.GetSnapshot(workflowId);
            if (s is { IsCompleted: true })
            {
                return;
            }

            await Task.Delay(20).ConfigureAwait(false);
        }

        Assert.Fail("Workflow did not complete in time.");
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

    private static CompiledWorkflowDefinition CreateTwoStateChainWithInputMapping(
        IStateExecutor a,
        IStateExecutor b,
        InputMappingDefinition mappingForB)
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
            InputMappings = new Dictionary<string, InputMappingDefinition> { ["B"] = mappingForB },
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

    private static CompiledWorkflowDefinition CreateForkDefinitionWithInputMapping(
        IStateExecutor start,
        IStateExecutor a,
        IStateExecutor b,
        IReadOnlyDictionary<string, InputMappingDefinition> inputMappings)
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
            InputMappings = inputMappings,
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
    /// これにより Join1 は allOf 完了後にのみ <see cref="WorkflowEngine"/> から起動される。
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

    private static CompiledWorkflowDefinition CreateJoinThenNextWithInputMapping(
        IStateExecutor a,
        IStateExecutor b,
        IStateExecutor afterJoin,
        InputMappingDefinition mappingForAfterJoin)
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
            InputMappings = new Dictionary<string, InputMappingDefinition> { ["AfterJoin"] = mappingForAfterJoin },
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
