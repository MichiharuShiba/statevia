using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Actions.Abstractions.Catalog;

/// <summary>Catalog 登録時の実行エントリ（Phase 1 は InProcess のみ）。</summary>
/// <param name="Aliases">canonical 以外の解決用エイリアス。</param>
/// <param name="InProcessFactory">InProcess 実行時の <see cref="IStateExecutor"/> ファクトリ。</param>
public sealed record ActionCatalogEntry(
    IReadOnlyList<string>? Aliases = null,
    Func<IServiceProvider, IStateExecutor>? InProcessFactory = null);
