using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Visibility;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Service.Api.Application.Actions.Resolution;
using Statevia.Service.Api.Application.Actions.Versioning;
using Statevia.Service.Api.Application.Actions.Validation;
using Statevia.Service.Api.Application.Definition;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Definition.Validation;
using Statevia.Core.Engine.Execution;

namespace Statevia.Service.Api.Hosting;

/// <summary>
/// YAML を検証・コンパイルして CompiledWorkflowDefinition を返す。Action Catalog で action を検証・解決する。
/// <see cref="ValidateAndCompile"/> の JSON には <c>transitions</c> に加え <c>conditionalTransitions</c>・<c>stateInputs</c> を含め、条件遷移のデバッグ・UI 表示に利用できる。
/// </summary>
internal sealed class DefinitionCompilerService : IDefinitionCompilerService
{
    private static readonly System.Text.Json.JsonSerializerOptions s_compiledJsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    private readonly IDefinitionLoadStrategy _definitionLoadStrategy;
    private readonly IActionCatalog _actionCatalog;
    private readonly IActionVisibilityResolver _visibilityResolver;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DefinitionCompilerService> _logger;

    /// <summary>
    /// 定義コンパイラサービスを構築する。
    /// </summary>
    /// <param name="actionCatalog">Action Catalog。</param>
    /// <param name="visibilityResolver">Visibility 判定。</param>
    /// <param name="definitionLoadStrategy">YAML ロード戦略。</param>
    /// <param name="serviceProvider">状態実行器ファクトリ生成用。</param>
    /// <param name="logger">Schema 未提供 action の warning ログ用。</param>
    public DefinitionCompilerService(
        IActionCatalog actionCatalog,
        IActionVisibilityResolver visibilityResolver,
        IDefinitionLoadStrategy definitionLoadStrategy,
        IServiceProvider serviceProvider,
        ILogger<DefinitionCompilerService> logger)
    {
        _actionCatalog = actionCatalog;
        _visibilityResolver = visibilityResolver;
        _definitionLoadStrategy = definitionLoadStrategy ?? throw new ArgumentNullException(nameof(definitionLoadStrategy));
        _serviceProvider = serviceProvider;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public (CompiledWorkflowDefinition Compiled, string CompiledJson) ValidateAndCompile(
        string name,
        string yaml,
        Guid? tenantId = null)
    {
        var def = _definitionLoadStrategy.Load(yaml);
        var compileBindings = ModuleActionCompileBinder.Bind(def, _actionCatalog);
        def = ResolveActionNames(def);
        var l1 = Level1Validator.Validate(def);
        if (!l1.IsValid)
        {
            throw new ArgumentException("Level 1 validation failed: " + string.Join("; ", l1.Errors));
        }

        var l2 = Level2Validator.Validate(def);
        if (!l2.IsValid)
        {
            throw new ArgumentException("Level 2 validation failed: " + string.Join("; ", l2.Errors));
        }

        ValidateRegisteredActions(compileBindings.StateActionBindings, tenantId);
        ValidateActionInputs(def, compileBindings.StateActionBindings);

        var factory = new ActionExecutorFactory(
            def,
            compileBindings.StateActionBindings,
            _actionCatalog,
            _serviceProvider);
        var compiler = new DefinitionCompiler(factory);
        var compiled = compiler.Compile(
            def,
            compileBindings.ResolvedModules,
            compileBindings.StateActionBindings);
        var compiledJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            compiled.Name,
            initialState = compiled.InitialState,
            transitions = compiled.Transitions,
            conditionalTransitions = compiled.ConditionalTransitions,
            forkTable = compiled.ForkTable,
            joinTable = compiled.JoinTable,
            waitTable = compiled.WaitTable,
            stateInputs = compiled.StateInputs,
            stateOutputs = compiled.StateOutputs,
            resolvedModules = compiled.ResolvedModules,
            stateActionBindings = compiled.StateActionBindings,
        }, s_compiledJsonOptions);
        return (compiled, compiledJson);
    }

    /// <inheritdoc />
    public CompiledWorkflowDefinition RestoreFromStoredVersion(string sourceYaml, string compiledJson)
    {
        var def = _definitionLoadStrategy.Load(sourceYaml);
        var compileBindings = StoredDefinitionBindingResolver.Resolve(def, compiledJson, _actionCatalog);
        def = ResolveActionNames(def);
        ValidateRegisteredActions(compileBindings.StateActionBindings, tenantId: null);
        ValidateActionInputs(def, compileBindings.StateActionBindings);
        var factory = new ActionExecutorFactory(
            def,
            compileBindings.StateActionBindings,
            _actionCatalog,
            _serviceProvider);
        return CompiledDefinitionJsonReader.Read(
            compiledJson,
            factory,
            compileBindings.ResolvedModules,
            compileBindings.StateActionBindings);
    }

    private static WorkflowDefinition ResolveActionNames(WorkflowDefinition def) =>
        ActionNameResolver.Resolve(def);

    private void ValidateRegisteredActions(
        IReadOnlyDictionary<string, Statevia.Core.Engine.Definition.StateActionBinding> bindings,
        Guid? tenantId)
    {
        foreach (var (stateName, binding) in bindings)
        {
            ActionDescriptor? descriptor = null;
            if (!TryGetVersionedDescriptor(binding, out descriptor)
                && !_actionCatalog.TryGetDescriptor(binding.LogicalActionId, out descriptor))
            {
                throw new ArgumentException(
                    $"Unknown action '{binding.LogicalActionId}' in state '{stateName}'.");
            }

            if (descriptor is null)
            {
                throw new ArgumentException(
                    $"Unknown action '{binding.LogicalActionId}' in state '{stateName}'.");
            }

            if (tenantId is not null
                && !_visibilityResolver.CanUse(tenantId.Value.ToString("D"), descriptor))
            {
                throw new ArgumentException(
                    $"Action '{binding.LogicalActionId}' is not visible to the current tenant in state '{stateName}'.");
            }
        }
    }

    private bool TryGetVersionedDescriptor(
        Statevia.Core.Engine.Definition.StateActionBinding binding,
        out ActionDescriptor? descriptor)
    {
        descriptor = null;
        if (binding.ResolvedModuleVersion is not { Length: > 0 } version
            || binding.ModuleId is not { Length: > 0 } moduleId
            || binding.ActionName is not { Length: > 0 } actionName)
        {
            return false;
        }

        return _actionCatalog.TryGetDescriptor(moduleId, version, actionName, out descriptor);
    }

    private void ValidateActionInputs(
        WorkflowDefinition def,
        IReadOnlyDictionary<string, Statevia.Core.Engine.Definition.StateActionBinding> bindings)
    {
        var errors = new List<ActionInputValidationError>();
        foreach (var (stateName, binding) in bindings)
        {
            if (!def.States.TryGetValue(stateName, out var state))
            {
                continue;
            }

            var actionId = binding.LogicalActionId;
            if (!TryGetPublication(binding, out var publication) || publication is null)
            {
                HandleMissingPublication(stateName, actionId, errors);
                continue;
            }

            errors.AddRange(ActionInputSchemaValidator.Validate(
                stateName,
                actionId,
                state.Input,
                publication.SchemaBundle.InputSchema.RootElement));
        }

        if (errors.Count > 0)
        {
            throw new ActionInputSchemaValidationException(errors);
        }
    }

    private bool TryGetPublication(
        Statevia.Core.Engine.Definition.StateActionBinding binding,
        out Statevia.Core.Actions.Abstractions.Publication.ActionPublication? publication)
    {
        publication = null;
        if (binding.ResolvedModuleVersion is { Length: > 0 } version
            && binding.ModuleId is { Length: > 0 } moduleId
            && binding.ActionName is { Length: > 0 } actionName)
        {
            return _actionCatalog.TryGetPublication(moduleId, version, actionName, out publication);
        }

        return _actionCatalog.TryGetPublication(binding.LogicalActionId, out publication);
    }

    private void HandleMissingPublication(
        string stateName,
        string actionId,
        List<ActionInputValidationError> errors)
    {
        if (ShouldRequirePublication(actionId))
        {
            errors.Add(new ActionInputValidationError(
                stateName,
                actionId,
                "$.input",
                $"Action '{actionId}' has no registered input schema."));
            return;
        }

        DefinitionCompilerLogMessages.ActionInputSchemaMissing(_logger, stateName, actionId);
    }

    private bool ShouldRequirePublication(string actionId)
    {
        if (ActionInputSchemaValidationOptions.RequireSchemaForUnpublishedActions)
        {
            return true;
        }

        if (!ActionInputSchemaValidationOptions.RequireSchemaForModuleActions)
        {
            return false;
        }

        if (!_actionCatalog.TryGetDescriptor(actionId, out var descriptor))
        {
            return false;
        }

        return descriptor.Visibility != ActionVisibility.Builtin;
    }

    /// <summary>起動時に組み込みアクションを Catalog へ登録する。</summary>
    /// <param name="catalog">登録先 Catalog。</param>
    public static void RegisterBuiltinActions(IActionCatalog catalog) =>
        BuiltinActionRegistrar.Register(catalog);
}
