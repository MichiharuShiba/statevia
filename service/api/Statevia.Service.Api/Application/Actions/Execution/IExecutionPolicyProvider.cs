using Statevia.Actions.Abstractions.Execution;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>実行コンテキストに対して、適用すべき階層別ポリシーを供給する。</summary>
/// <remarks>
/// <para>複数 provider を DI 登録でき、<see cref="ConfigurableExecutionPolicy"/> が base 結果へ
/// 最厳優先（緩和不可）で合成する。各 provider は自分が知る階層分のみ返せばよい。</para>
/// <para>保存先は実装依存（MVP は appsettings、将来 DB 等へ差し替え可能）。</para>
/// </remarks>
internal interface IExecutionPolicyProvider
{
    /// <summary>当該コンテキストに適用する階層別ポリシーを返す。</summary>
    /// <param name="context">Mode 決定の実行コンテキスト。</param>
    /// <returns>適用するスコープ別ポリシー。該当なしは空。</returns>
    IReadOnlyList<ScopedExecutionPolicy> GetPolicies(ActionExecutionContext context);
}
