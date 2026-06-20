using Microsoft.Extensions.DependencyInjection;
using Statevia.Actions.Abstractions.Catalog;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Modules;

/// <summary>Action Module が Catalog 登録用に返す 1 Action 分の情報。</summary>
/// <param name="ActionId">canonical actionId（推奨: <c>{moduleId}.{actionName}</c>）。</param>
/// <param name="ExecutorFactory">InProcess 実行器ファクトリ。</param>
/// <param name="Descriptor">任意の Descriptor 上書き（未指定時は ModuleHost が生成）。</param>
public sealed record ModuleActionRegistration(
    string ActionId,
    Func<IServiceProvider, IStateExecutor> ExecutorFactory,
    ActionDescriptor? Descriptor = null);
