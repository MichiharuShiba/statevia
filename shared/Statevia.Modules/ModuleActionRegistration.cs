using Statevia.Actions.Abstractions.Publication;
using Statevia.Core.Engine.Abstractions;
using CatalogActionDescriptor = Statevia.Actions.Abstractions.Catalog.ActionDescriptor;

namespace Statevia.Modules;

/// <summary>Action Module が Catalog 登録用に返す 1 Action 分の情報。</summary>
/// <param name="ActionId">canonical actionId（推奨: <c>{moduleId}.{actionName}</c>）。</param>
/// <param name="ExecutorFactory">InProcess 実行器ファクトリ。</param>
/// <param name="Descriptor">任意の Catalog Descriptor 上書き（未指定時は ModuleHost が生成）。</param>
/// <param name="Publication">input/output schema と UI メタデータ（Module action では compile 時必須）。</param>
public sealed record ModuleActionRegistration(
    string ActionId,
    Func<IServiceProvider, IStateExecutor> ExecutorFactory,
    CatalogActionDescriptor? Descriptor = null,
    ActionPublication? Publication = null);
