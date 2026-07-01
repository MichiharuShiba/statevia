namespace Statevia.Core.Api.Application.Actions.Execution;

/// <summary>実行ポリシーを適用する階層スコープ。</summary>
/// <remarks>
/// 上位（Organization）から下位（Tenant）まで段階的に制約を重ねる想定。
/// 本フェーズの実装は <see cref="Tenant"/> のみ。Org / Project / Environment は将来拡張枠。
/// </remarks>
internal enum ExecutionPolicyScope
{
    /// <summary>組織スコープ（将来）。</summary>
    Organization,

    /// <summary>プロジェクトスコープ（将来）。</summary>
    Project,

    /// <summary>環境スコープ（将来）。</summary>
    Environment,

    /// <summary>テナントスコープ。</summary>
    Tenant,
}
