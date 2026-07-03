using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Statevia.Core.Engine.FSM;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

public sealed partial class ExecutionEngineLoggingTests
{
    /// <summary>正常完走時に Execution 開始・State スケジュール・State 完了（ElapsedMs 付き）が Information ログに残ることを検証する。</summary>
    [Fact]
    public async Task Logging_CapturesExecutionStart_StateScheduleComplete_WithElapsedMs()
    {
        // Arrange
        var sink = new ListLogger();
        var def = CreateMinimalDefinition();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1, executionLogger: sink);

        // Act
        var id = engine.Start(def);
        await Task.Delay(200).ConfigureAwait(false);

        // Assert
        Assert.Contains(sink.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("Execution started", StringComparison.Ordinal) && e.Message.Contains(id, StringComparison.Ordinal));
        Assert.Contains(sink.Entries, e => e.Message.Contains("State scheduled", StringComparison.Ordinal) && e.Message.Contains("Start", StringComparison.Ordinal));
        var completed = Assert.Single(sink.Entries, e => e.Message.Contains("State completed", StringComparison.Ordinal) && e.Message.Contains("ElapsedMs=", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Information, completed.Level);
        Assert.Contains("Fact=Completed", completed.Message, StringComparison.Ordinal);
    }

    /// <summary>状態が例外で失敗したときに State execute failed・Fact=Failed・Execution terminal failure がログに残ることを検証する。</summary>
    [Fact]
    public async Task Logging_StateFailure_EmitsErrorAndCompletedWithFailedFact()
    {
        // Arrange
        var sink = new ListLogger();
        var def = CreateDefinitionWithFailingState();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1, executionLogger: sink);

        // Act
        engine.Start(def);
        await Task.Delay(200).ConfigureAwait(false);

        // Assert
        Assert.Contains(sink.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("State execute failed", StringComparison.Ordinal));
        Assert.Contains(sink.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("Fact=Failed", StringComparison.Ordinal));
        Assert.Contains(sink.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Execution terminal failure", StringComparison.Ordinal));
    }

    /// <summary>Join 完了ログに ElapsedMs が含まれないことを検証する。</summary>
    [Fact]
    public async Task Logging_JoinCompletedLine_OmitsElapsedMs()
    {
        // Arrange
        var sink = new ListLogger();
        var def = CreateDefinitionWithForkJoin();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 2, executionLogger: sink);

        // Act
        engine.Start(def);
        await Task.Delay(600).ConfigureAwait(false);

        // Assert
        Assert.Contains(sink.Entries, e => e.Message.Contains("Kind=Join", StringComparison.Ordinal));
        var joinCompletes = sink.Entries.FindAll(e =>
            e.Message.Contains("State completed", StringComparison.Ordinal) &&
            e.Message.Contains("Fact=Joined", StringComparison.Ordinal));
        Assert.NotEmpty(joinCompletes);
        Assert.DoesNotContain(joinCompletes, e => e.Message.Contains("ElapsedMs", StringComparison.Ordinal));
    }

    /// <summary>state input の path が raw に存在しないとき Input evaluation warning が出ることを検証する（STV-405）。</summary>
    [Fact]
    public async Task Logging_StateInputPathSegmentMissing_EmitsWarning()
    {
        // Arrange
        var sink = new ListLogger();
        var def = new CompiledWorkflowDefinition
        {
            Name = "InputWarn",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["A"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "B" } },
                ["B"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
            },
            ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
            JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
            WaitTable = new Dictionary<string, string>(),
            StateInputs = new Dictionary<string, StateInputDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["B"] = new StateInputDefinition { Path = "$.missing" }
            },
            InitialState = "A",
            StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
            {
                ["A"] = DefaultStateExecutor.Create(new ConstantOutputState(new Dictionary<string, object?>())),
                ["B"] = DefaultStateExecutor.Create(new ImmediateState())
            })
        };
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1, executionLogger: sink);

        // Act
        engine.Start(def);
        await Task.Delay(300).ConfigureAwait(false);

        // Assert
        Assert.Contains(
            sink.Entries,
            static e =>
                e.Level == LogLevel.Warning &&
                e.Message.Contains("Input evaluation warning", StringComparison.Ordinal) &&
                e.Message.Contains("Reason=PathSegmentMissing", StringComparison.Ordinal) &&
                e.Message.Contains("InputKey=$.missing", StringComparison.Ordinal) &&
                e.Message.Contains("StateName=B", StringComparison.Ordinal));
    }

    /// <summary>FSM に Completed 遷移が無く停止するとき No transition の Warning が出ることを検証する（STV-405）。</summary>
    [Fact]
    public async Task Logging_NoFsmTransitionStall_EmitsWarning()
    {
        // Arrange
        var sink = new ListLogger();
        var def = new CompiledWorkflowDefinition
        {
            Name = "NoTransition",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["Start"] = new Dictionary<string, TransitionTarget>()
            },
            ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
            JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
            WaitTable = new Dictionary<string, string>(),
            InitialState = "Start",
            StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
            {
                ["Start"] = DefaultStateExecutor.Create(new ImmediateState())
            })
        };
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1, executionLogger: sink);

        // Act
        engine.Start(def);
        await Task.Delay(200).ConfigureAwait(false);

        // Assert
        Assert.Contains(
            sink.Entries,
            static e =>
                e.Level == LogLevel.Warning &&
                e.Message.Contains("No transition", StringComparison.Ordinal) &&
                e.Message.Contains("StateName=Start", StringComparison.Ordinal) &&
                e.Message.Contains("Fact=Completed", StringComparison.Ordinal));
    }

    /// <summary>ログ実装が例外を投げても実行完走が継続することを検証する（STV-404 SafeLog）。</summary>
    [Fact]
    public async Task Logging_ThrowingLogger_DoesNotFailExecution()
    {
        // Arrange
        var def = CreateMinimalDefinition();
        using var engine = ExecutionEngineTestHarness.Create(
            maxParallelism: 1,
            executionLogger: new ThrowingExecutionEngineLogger());

        // Act
        var executionId = engine.Start(def);
        await Task.Delay(200).ConfigureAwait(false);

        // Assert
        var snapshot = engine.GetSnapshot(executionId);
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
    }

    /// <summary>ctx.Logger のログに ExecutionId/StateName の文脈が自動付与されることを検証する（STV-406）。</summary>
    [Fact]
    public async Task Logging_StateContextLogger_ContainsExecutionAndStateScope()
    {
        // Arrange
        var sink = new ListLogger();
        var def = new CompiledWorkflowDefinition
        {
            Name = "CtxLogger",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["Start"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
            },
            ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
            JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
            WaitTable = new Dictionary<string, string>(),
            InitialState = "Start",
            StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
            {
                ["Start"] = DefaultStateExecutor.Create(new LoggingState())
            })
        };
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1, executionLogger: sink);

        // Act
        var executionId = engine.Start(def);
        await Task.Delay(250).ConfigureAwait(false);

        // Assert
        Assert.Contains(
            sink.Entries,
            e =>
                e.Level == LogLevel.Information &&
                e.Message.Contains("StateContext user log", StringComparison.Ordinal) &&
                e.Message.Contains($"ExecutionId={executionId}", StringComparison.Ordinal) &&
                e.Message.Contains("StateName=Start", StringComparison.Ordinal));
    }

    private sealed class ThrowingExecutionEngineLogger : ILogger<ExecutionEngine.ExecutionEngineLogger>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("logger failure");

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class ListLogger : ILogger<ExecutionEngine.ExecutionEngineLogger>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];
        private readonly AsyncLocal<Stack<object>> _scopes = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            var stack = _scopes.Value ??= new Stack<object>();
            stack.Push(state);
            return new Scope(() => stack.Pop());
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            var stack = _scopes.Value;
            if (stack is not null && stack.Count > 0)
            {
                foreach (var scope in stack)
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> structuredScope)
                    {
                        foreach (var (key, value) in structuredScope)
                        {
                            message += $" {key}={value}";
                        }
                    }
                }
            }

            lock (Entries)
            {
                Entries.Add((logLevel, message, exception));
            }
        }

        private sealed class Scope : IDisposable
        {
            private readonly Action _onDispose;

            public Scope(Action onDispose) => _onDispose = onDispose;

            public void Dispose() => _onDispose();
        }
    }

    private static CompiledWorkflowDefinition CreateMinimalDefinition() => new()
    {
        Name = "Minimal",
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["Start"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
        },
        ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
        JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
        WaitTable = new Dictionary<string, string>(),
        InitialState = "Start",
        StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
        {
            ["Start"] = DefaultStateExecutor.Create(new ImmediateState())
        })
    };

    private static CompiledWorkflowDefinition CreateDefinitionWithFailingState() => new()
    {
        Name = "Fail",
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["Bad"] = new Dictionary<string, TransitionTarget>()
        },
        ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
        JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
        WaitTable = new Dictionary<string, string>(),
        InitialState = "Bad",
        StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
        {
            ["Bad"] = DefaultStateExecutor.Create(new ThrowingState())
        })
    };

    private static CompiledWorkflowDefinition CreateDefinitionWithForkJoin() => new()
    {
        Name = "ForkJoin",
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["Start"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Fork = new[] { "A", "B" } } },
            ["A"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "Join1" } },
            ["B"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "Join1" } },
            ["Join1"] = new Dictionary<string, TransitionTarget> { ["Joined"] = new TransitionTarget { End = true } }
        },
        ForkTable = new Dictionary<string, IReadOnlyList<string>> { ["Start"] = new[] { "A", "B" } },
        JoinTable = new Dictionary<string, IReadOnlyList<string>> { ["Join1"] = new[] { "A", "B" } },
        WaitTable = new Dictionary<string, string>(),
        InitialState = "Start",
        StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
        {
            ["Start"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["A"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["B"] = DefaultStateExecutor.Create(new ImmediateState())
        })
    };

    private sealed class ImmediateState : IState<Unit, Unit>
    {
        public Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct) => Task.FromResult(Unit.Value);
    }

    private sealed class ConstantOutputState : IState<object?, object?>
    {
        private readonly object? _output;

        public ConstantOutputState(object? output) => _output = output;

        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) =>
            Task.FromResult(_output);
    }

    private static void LogStateContextUserMessage(ILogger logger) =>
        logger.LogInformation("StateContext user log");

    private sealed class LoggingState : IState<Unit, Unit>
    {
        public Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct)
        {
            LogStateContextUserMessage(ctx.Logger);
            return Task.FromResult(Unit.Value);
        }
    }

    private sealed class ThrowingState : IState<Unit, Unit>
    {
        public Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct) => throw new InvalidOperationException("boom");
    }
}
