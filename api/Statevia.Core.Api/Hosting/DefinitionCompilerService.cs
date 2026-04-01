using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Application.Actions;
using Statevia.Core.Api.Application.Actions.Abstractions;
using Statevia.Core.Api.Application.Actions.Builtins;
using Statevia.Core.Api.Application.Definition;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Definition.Validation;
using Statevia.Core.Engine.Execution;

namespace Statevia.Core.Api.Hosting;

/// <summary>YAML を検証・コンパイルして CompiledWorkflowDefinition を返す。Action Registry で action を検証・解決する。</summary>
public sealed class DefinitionCompilerService : IDefinitionCompilerService
{
    private readonly IDefinitionLoadStrategy _definitionLoadStrategy;
    private readonly IActionRegistry _actionRegistry;

    public DefinitionCompilerService(IActionRegistry actionRegistry, IDefinitionLoadStrategy definitionLoadStrategy)
    {
        _actionRegistry = actionRegistry;
        _definitionLoadStrategy = definitionLoadStrategy ?? throw new ArgumentNullException(nameof(definitionLoadStrategy));
    }

    public (CompiledWorkflowDefinition Compiled, string CompiledJson) ValidateAndCompile(string name, string yaml)
    {
        var def = _definitionLoadStrategy.Load(yaml);
        var l1 = Level1Validator.Validate(def);
        if (!l1.IsValid)
            throw new ArgumentException("Level 1 validation failed: " + string.Join("; ", l1.Errors));
        var l2 = Level2Validator.Validate(def);
        if (!l2.IsValid)
            throw new ArgumentException("Level 2 validation failed: " + string.Join("; ", l2.Errors));

        ValidateRegisteredActions(def);

        var factory = new ActionExecutorFactory(def, _actionRegistry);
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

    private void ValidateRegisteredActions(WorkflowDefinition def)
    {
        foreach (var (stateName, state) in def.States)
        {
            if (string.IsNullOrWhiteSpace(state.Action))
                continue;

            var id = state.Action.Trim();
            if (!_actionRegistry.Exists(id))
                throw new ArgumentException($"Unknown action '{id}' in state '{stateName}'.");
        }
    }

    /// <summary>起動時に組み込みアクションを Registry へ登録する。</summary>
    public static void RegisterBuiltinActions(IActionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(WellKnownActionIds.NoOp, DefaultStateExecutor.Create(new NoOpState()));
    }
}
