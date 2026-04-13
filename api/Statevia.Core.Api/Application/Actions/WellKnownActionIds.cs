namespace Statevia.Core.Api.Application.Actions;

/// <summary>組み込みおよび既定で解決するアクション ID。</summary>
public static class WellKnownActionIds
{
    /// <summary>アクション未指定状態で用いるダミー完了（従来の no-op と同等）。</summary>
    public const string NoOp = "noop";

    /// <summary>定義検証・UI デモ用。約5秒待機してから完了する（入出力なし）。</summary>
    public const string Delay5s = "delay5s";
}
