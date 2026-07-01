namespace Statevia.Service.Api.Application.Actions.Modules;

/// <summary>Action Module の発見元。取得経路のみを担当し、DLL load は行わない。</summary>
/// <remarks>
/// <para>
/// 複数 Source は <see cref="CompositeModuleSource"/> が <see cref="Priority"/> 昇順で集約する。
/// 同一 Module（modules ルート直下のディレクトリ名が一致）が複数 Source から発見された場合は
/// 高優先（数値が小さい）側が勝ち、低優先側は警告ログを残して破棄する。
/// </para>
/// </remarks>
internal interface IModuleSource
{
    /// <summary>集約時の優先度。<b>小さいほど優先</b>し、DI 登録順に依存しない。</summary>
    /// <remarks>同値の場合は <see cref="DiscoveredModule.SourceLabel"/> 昇順で安定化する。</remarks>
    int Priority { get; }

    /// <summary>利用可能な Action Module を列挙する。</summary>
    /// <param name="cancellationToken">キャンセル。</param>
    Task<IReadOnlyList<DiscoveredModule>> DiscoverAsync(CancellationToken cancellationToken);
}
