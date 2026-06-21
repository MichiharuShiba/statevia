namespace Statevia.Core.Api.Application.Actions.Modules;

/// <summary>Action Module の発見元。取得経路のみを担当し、DLL load は行わない。</summary>
internal interface IModuleSource
{
    /// <summary>利用可能な Action Module を列挙する。</summary>
    /// <param name="cancellationToken">キャンセル。</param>
    Task<IReadOnlyList<DiscoveredModule>> DiscoverAsync(CancellationToken cancellationToken);
}
