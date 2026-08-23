namespace Statevia.Service.Api.Application.Actions;

/// <summary>残す builtin action の canonical ID 定数。</summary>
/// <remarks>
/// 短名ヘルパーは持たない。YAML の <c>action</c> 省略時のみ <see cref="NoOpCanonical"/> を使う。
/// REST / notify はリファレンス Module 側の ID であり、本クラスには置かない。
/// </remarks>
internal static class WellKnownActionIds
{
    /// <summary>builtin canonical ID のプレフィックス。</summary>
    public const string BuiltinPrefix = "statevia.action.builtin.";

    /// <summary>implicit noop の canonical ID。</summary>
    public const string NoOpCanonical = BuiltinPrefix + "execution.noop";

    /// <summary>時間待機 builtin。</summary>
    public const string Sleep = BuiltinPrefix + "execution.sleep";

    /// <summary>実行スコープ内シグナル builtin。</summary>
    public const string Signal = BuiltinPrefix + "execution.signal";

    /// <summary>システムイベント発行 builtin。</summary>
    public const string Publish = BuiltinPrefix + "event.publish";

    /// <summary>子ワークフロー起動 builtin。</summary>
    public const string Workflow = BuiltinPrefix + "workflow.invoke";
}
