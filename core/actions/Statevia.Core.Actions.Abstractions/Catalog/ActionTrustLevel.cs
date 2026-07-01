namespace Statevia.Core.Actions.Abstractions.Catalog;

/// <summary>Action / Module の信頼レベル。</summary>
public enum ActionTrustLevel
{
    /// <summary>プラットフォーム組み込み。</summary>
    Trusted,

    /// <summary>署名が有効で、運営が信頼した署名者によるサードパーティ。</summary>
    Verified,

    /// <summary>
    /// 署名が有効だが運営が署名者を信頼していないサードパーティ。
    /// 「改ざんされていないこと」のみを保証し、安全（信頼）保証ではない。実行緩和の対象外。
    /// </summary>
    Signed,

    /// <summary>コミュニティ Module（既定。署名なし）。</summary>
    Community,

    /// <summary>未検証・低信頼。</summary>
    Untrusted,
}
