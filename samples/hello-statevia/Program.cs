using Statevia.Core.Abstractions;
using Statevia.Core.Definition;
using Statevia.Core.Definition.Validation;
using Statevia.Core.Engine;
using Statevia.Core.Execution;

var loader = new DefinitionLoader();
var content = await File.ReadAllTextAsync("hello.yaml");
var def = loader.Load(content);

var level1 = Level1Validator.Validate(def);
if (!level1.IsValid)
{
    Console.WriteLine("Level 1 validation failed:");
    foreach (var e in level1.Errors) Console.WriteLine("  - " + e);
    return 1;
}

var level2 = Level2Validator.Validate(def);
if (!level2.IsValid)
{
    Console.WriteLine("Level 2 validation failed:");
    foreach (var e in level2.Errors) Console.WriteLine("  - " + e);
    return 1;
}

var executors = new Dictionary<string, IStateExecutor>(StringComparer.OrdinalIgnoreCase)
{
    ["Start"] = DefaultStateExecutor.Create(new StartState()),
    ["Prepare"] = DefaultStateExecutor.Create(new PrepareState()),
    ["AskUser"] = DefaultStateExecutor.Create(new AskUserState()),
    ["Work"] = DefaultStateExecutor.Create(new WorkState()),
    ["End"] = DefaultStateExecutor.Create(new EndState())
};

var factory = new DictionaryStateExecutorFactory(executors);
var compiler = new DefinitionCompiler(factory);
var compiled = compiler.Compile(def);

// README 3.3 に準拠
using var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 2 });
var id = engine.Start(compiled);

Console.WriteLine($"Workflow started: {id}");

// 待機状態の再開（README 参照: engine.PublishEvent("UserApproved")）
await Task.Delay(100).ConfigureAwait(false);
engine.PublishEvent("UserApproved");

await Task.Delay(1500).ConfigureAwait(false);
var snapshot = engine.GetSnapshot(id);
Console.WriteLine($"Active: [{string.Join(", ", snapshot?.ActiveStates ?? Array.Empty<string>())}]");
Console.WriteLine($"Completed: {snapshot?.IsCompleted}");
Console.WriteLine($"Graph: {engine.ExportExecutionGraph(id)}");

return 0;

sealed class StartState : IState<Unit, Unit>
{
    public Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct) => Task.FromResult(Unit.Value);
}

sealed class PrepareState : IState<Unit, string>
{
    public async Task<string> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct)
    {
        await Task.Delay(500, ct).ConfigureAwait(false);
        return "prepared";
    }
}

sealed class AskUserState : IState<Unit, bool>
{
    public async Task<bool> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct)
    {
        await ctx.Events.WaitAsync("UserApproved", ct).ConfigureAwait(false);
        return true;
    }
}

sealed class WorkState : IState<object, Unit>
{
    public Task<Unit> ExecuteAsync(StateContext ctx, object input, CancellationToken ct)
    {
        if (input is IReadOnlyDictionary<string, object?> dict)
        {
            var prepared = dict.TryGetValue("Prepare", out var p) ? p?.ToString() : "";
            var approved = dict.TryGetValue("AskUser", out var a) && a is bool b && b;
            Console.WriteLine($"Work: prepared={prepared}, approved={approved}");
        }
        return Task.FromResult(Unit.Value);
    }
}

sealed class EndState : IState<Unit, Unit>
{
    public Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct) => Task.FromResult(Unit.Value);
}
