using Statevia.Actions.Abstractions.Execution;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>1 スコープ分の実行ポリシー設定（appsettings バインド用）。</summary>
internal sealed class ScopedExecutionPolicyOptions
{
    /// <summary>
    /// 最低実行モード（隔離下限）。<see cref="ActionExecutionMode"/> 名で設定する。
    /// base（TrustLevel × Environment）下限を緩和できず、より厳しい場合のみ作用する。
    /// </summary>
    public ActionExecutionMode? MinimumMode { get; set; }
}
