using Microsoft.Extensions.Logging;

namespace Statevia.Infrastructure.Modules;

/// <summary>複数の <see cref="IModuleSource"/> を決定的に集約する Source。</summary>
/// <remarks>
/// <para>
/// 各 Source の <see cref="IModuleSource.Priority"/> 昇順（小さいほど優先）で discover 結果を連結し、
/// 同一 Module（<see cref="DiscoveredModule.ModuleDirectoryName"/> が大文字小文字無視で一致）が
/// 複数 Source から見つかった場合は高優先側を採用、低優先側は警告ログを残して破棄する。
/// </para>
/// <para>
/// 同 <see cref="IModuleSource.Priority"/> の重複は <see cref="DiscoveredModule.SourceLabel"/> 昇順
/// （<see cref="StringComparer.OrdinalIgnoreCase"/>、<see langword="null"/> は先頭）で tie-break し、
/// DI 登録順に依存しない安定した結果を返す。
/// </para>
/// <para>
/// ModuleHost が consume する単一 Source として DI で注入される。自身は子 Source として集約対象には
/// 含めない（<see cref="Priority"/> は未使用）。万一の自己参照登録に備え、構築時に自身を除外する。
/// </para>
/// </remarks>
internal sealed class CompositeModuleSource : IModuleSource
{
    private readonly IReadOnlyList<IModuleSource> _sources;
    private readonly ILogger<CompositeModuleSource> _logger;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="sources">集約対象の Source 群（自身が含まれても防御的に除外する）。</param>
    /// <param name="logger">ログ。</param>
    public CompositeModuleSource(
        IEnumerable<IModuleSource> sources,
        ILogger<CompositeModuleSource> logger)
    {
        ArgumentNullException.ThrowIfNull(sources);
        _sources = sources.Where(source => source is not CompositeModuleSource).ToList();
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>Composite 自身は集約対象ではないため、本値は使用されない。</remarks>
    public int Priority => 0;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscoveredModule>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var discoveredBySource = new List<(int Priority, DiscoveredModule Module)>();
        foreach (var source in _sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ShouldDiscover(source))
            {
                continue;
            }

            var modules = await source.DiscoverAsync(cancellationToken).ConfigureAwait(false);
            discoveredBySource.AddRange(modules.Select(module => (source.Priority, module)));
        }

        var ordered = discoveredBySource
            .OrderBy(entry => entry.Priority)
            .ThenBy(entry => entry.Module.SourceLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Module.ModuleDirectoryName, StringComparer.OrdinalIgnoreCase);

        var winners = new Dictionary<string, DiscoveredModule>(StringComparer.OrdinalIgnoreCase);
        var result = new List<DiscoveredModule>();
        foreach (var (_, module) in ordered)
        {
            if (winners.TryGetValue(module.ModuleDirectoryName, out var winner))
            {
                CompositeModuleSourceLog.DuplicateModuleDropped(
                    _logger,
                    module.ModuleDirectoryName,
                    module.SourceLabel,
                    winner.SourceLabel);
                continue;
            }

            winners[module.ModuleDirectoryName] = module;
            result.Add(module);
        }

        return result;
    }

    private static bool ShouldDiscover(IModuleSource source) =>
        source is FilesystemModuleSource
            ? ModuleDiscoveryContext.DiscoverFilesystem
            : ModuleDiscoveryContext.DiscoverRemote;
}

/// <summary><see cref="CompositeModuleSource"/> の構造化ログ。</summary>
internal static partial class CompositeModuleSourceLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Dropping duplicate module '{ModuleDirectoryName}' from source '{DroppedSource}'; kept higher-priority source '{KeptSource}'")]
    public static partial void DuplicateModuleDropped(
        ILogger logger,
        string moduleDirectoryName,
        string? droppedSource,
        string? keptSource);
}
