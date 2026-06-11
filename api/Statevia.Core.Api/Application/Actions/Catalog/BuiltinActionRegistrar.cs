using Statevia.Actions.Abstractions.Catalog;
using Statevia.Actions.Abstractions.Execution;
using Statevia.Core.Api.Application.Actions.Builtins;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Execution;

namespace Statevia.Core.Api.Application.Actions.Catalog;

/// <summary>組み込み Action を Catalog へ登録する。</summary>
internal static class BuiltinActionRegistrar
{
    private const string BuiltinModuleId = "statevia.builtin";
    private const string BuiltinVersion = "1.0.0";

    /// <summary>noop / delay5s 等の Builtin を Catalog へ登録する。</summary>
    /// <param name="catalog">登録先 Catalog。</param>
    public static void Register(IActionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        RegisterNoOp(catalog);
        RegisterDelay5s(catalog);
    }

    private static void RegisterNoOp(IActionCatalog catalog)
    {
        var factory = CreateFactory(new NoOpState());
        var descriptor = CreateBuiltinDescriptor(WellKnownActionIds.NoOpCanonical);

        catalog.Register(
            descriptor,
            new ActionCatalogEntry(
                Aliases: [WellKnownActionIds.NoOp],
                InProcessFactory: factory));
    }

    private static void RegisterDelay5s(IActionCatalog catalog)
    {
        var factory = CreateFactory(new DelayCompleteState(TimeSpan.FromSeconds(5)));
        var descriptor = CreateBuiltinDescriptor(WellKnownActionIds.Delay5s);

        catalog.Register(
            descriptor,
            new ActionCatalogEntry(InProcessFactory: factory));
    }

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
}
