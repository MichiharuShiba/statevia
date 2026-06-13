using Microsoft.Extensions.DependencyInjection;
using Statevia.Actions.Abstractions.Catalog;
using Statevia.Actions.Abstractions.Execution;
using Statevia.Core.Api.Application.Actions.Builtins;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Execution;

namespace Statevia.Core.Api.Application.Actions.Catalog;

/// <summary>Builtin action を Catalog へ登録する。</summary>
internal static class BuiltinActionRegistrar
{
    private const string BuiltinModuleId = "statevia.builtin";
    private const string BuiltinVersion = "1.0.0";

    /// <summary>全 Builtin を Catalog へ登録する。</summary>
    /// <param name="catalog">登録先 Catalog。</param>
    public static void Register(IActionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (catalog is not InMemoryActionCatalog memoryCatalog)
        {
            throw new ArgumentException(
                "Builtin registration requires InMemoryActionCatalog.",
                nameof(catalog));
        }

        RegisterNoOp(memoryCatalog);
        RegisterSleep(memoryCatalog);
        RegisterSignal(memoryCatalog);
        RegisterPublish(memoryCatalog);
        RegisterRest(memoryCatalog);
        RegisterNotification(memoryCatalog);
        RegisterWorkflow(memoryCatalog);
    }

    private static void RegisterNoOp(InMemoryActionCatalog catalog)
    {
        catalog.Register(
            CreateBuiltinDescriptor(WellKnownActionIds.NoOpCanonical),
            new ActionCatalogEntry(
                Aliases: [WellKnownActionIds.NoOp],
                InProcessFactory: CreateFactory(new NoOpState())),
            new ActionCapabilityMetadata(ActionCapabilityCategory.Transform, "No-op", IsExperimental: false));
    }

    private static void RegisterSleep(InMemoryActionCatalog catalog) =>
        catalog.Register(
            CreateBuiltinDescriptor(WellKnownActionIds.Sleep),
            new ActionCatalogEntry(InProcessFactory: CreateFactory(new SleepActionState())),
            new ActionCapabilityMetadata(ActionCapabilityCategory.Timing, "Sleep", IsExperimental: false));

    private static void RegisterSignal(InMemoryActionCatalog catalog) =>
        catalog.Register(
            CreateBuiltinDescriptor(WellKnownActionIds.Signal),
            new ActionCatalogEntry(InProcessFactory: CreateFactory(new SignalActionState())),
            new ActionCapabilityMetadata(ActionCapabilityCategory.Signal, "Signal", IsExperimental: false));

    private static void RegisterPublish(InMemoryActionCatalog catalog) =>
        catalog.Register(
            CreateBuiltinDescriptor(WellKnownActionIds.Publish),
            new ActionCatalogEntry(InProcessFactory: CreateFactory(new PublishActionState())),
            new ActionCapabilityMetadata(ActionCapabilityCategory.Event, "Publish", IsExperimental: false));

    private static void RegisterRest(InMemoryActionCatalog catalog) =>
        catalog.Register(
            CreateBuiltinDescriptor(WellKnownActionIds.Rest),
            new ActionCatalogEntry(InProcessFactory: CreateScopedFactory<RestActionState>()),
            new ActionCapabilityMetadata(ActionCapabilityCategory.Http, "REST", IsExperimental: false));

    private static void RegisterNotification(InMemoryActionCatalog catalog) =>
        catalog.Register(
            CreateBuiltinDescriptor(WellKnownActionIds.Notify),
            new ActionCatalogEntry(InProcessFactory: CreateScopedFactory<NotificationActionState>()),
            new ActionCapabilityMetadata(ActionCapabilityCategory.Notification, "Notification", IsExperimental: false));

    private static void RegisterWorkflow(InMemoryActionCatalog catalog) =>
        catalog.Register(
            CreateBuiltinDescriptor(WellKnownActionIds.Workflow),
            new ActionCatalogEntry(InProcessFactory: CreateScopedFactory<WorkflowActionState>()),
            new ActionCapabilityMetadata(ActionCapabilityCategory.Workflow, "Workflow", IsExperimental: true));

    private static ActionDescriptor CreateBuiltinDescriptor(string actionId) =>
        new()
        {
            ActionId = actionId,
            ModuleId = BuiltinModuleId,
            Version = BuiltinVersion,
            TrustLevel = ActionTrustLevel.Trusted,
            Source = ActionSourceKind.Builtin,
            OwnerTenantId = null,
            Visibility = ActionVisibility.Builtin,
            ExecutionHints = new ActionExecutionHints
            {
                PreferredMode = ActionExecutionMode.InProcess,
            },
        };

    private static Func<IServiceProvider, IStateExecutor> CreateFactory<TIn, TOut>(IState<TIn, TOut> state) =>
        _ => DefaultStateExecutor.Create(state);

    private static Func<IServiceProvider, IStateExecutor> CreateScopedFactory<TState>()
        where TState : class, IState<object?, object?> =>
        serviceProvider =>
            DefaultStateExecutor.Create(ActivatorUtilities.CreateInstance<TState>(serviceProvider));
}
