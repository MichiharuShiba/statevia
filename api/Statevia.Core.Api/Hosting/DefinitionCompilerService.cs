using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Definition.Validation;
using Statevia.Core.Engine.Execution;

namespace Statevia.Core.Api.Hosting;

/// <summary>YAML を検証・コンパイルして CompiledWorkflowDefinition を返す。API 用の汎用 State でコンパイルする。</summary>
public interface IDefinitionCompilerService
{
    (CompiledWorkflowDefinition Compiled, string CompiledJson) ValidateAndCompile(string name, string yaml);
}

public sealed class DefinitionCompilerService : IDefinitionCompilerService
{
    private readonly DefinitionLoader _loader = new();

    public (CompiledWorkflowDefinition Compiled, string CompiledJson) ValidateAndCompile(string name, string yaml)
    {
        var def = _loader.Load(yaml);
        var l1 = Level1Validator.Validate(def);
        if (!l1.IsValid)
            throw new ArgumentException("Level 1 validation failed: " + string.Join("; ", l1.Errors));
        var l2 = Level2Validator.Validate(def);
        if (!l2.IsValid)
            throw new ArgumentException("Level 2 validation failed: " + string.Join("; ", l2.Errors));

        var executors = new Dictionary<string, IStateExecutor>(StringComparer.OrdinalIgnoreCase);
        foreach (var stateName in def.States.Keys)
        {
            var stateDef = def.States[stateName];
            if (stateDef.Wait != null)
                executors[stateName] = DefaultStateExecutor.Create(new WaitOnlyState(stateDef.Wait.Event));
            else
                executors[stateName] = DefaultStateExecutor.Create(new NoOpState());
        }
        var factory = new DictionaryStateExecutorFactory(executors);
        var compiler = new DefinitionCompiler(factory);
        var compiled = compiler.Compile(def);
        var compiledJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            compiled.Name,
            initialState = compiled.InitialState,
            transitions = compiled.Transitions,
            forkTable = compiled.ForkTable,
            joinTable = compiled.JoinTable,
            waitTable = compiled.WaitTable
        });
        return (compiled, compiledJson);
    }

    private sealed class NoOpState : IState<Unit, Unit>
    {
        public Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct) => Task.FromResult(Unit.Value);
    }

    private sealed class WaitOnlyState : IState<Unit, Unit>
    {
        private readonly string _eventName;
        public WaitOnlyState(string eventName) => _eventName = eventName;
        public async Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct)
        {
            await ctx.Events.WaitAsync(_eventName, ct).ConfigureAwait(false);
            return Unit.Value;
        }
    }
}
