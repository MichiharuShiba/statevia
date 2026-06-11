namespace Statevia.Actions.Abstractions.Catalog;

/// <summary>Action / Module の信頼レベル。</summary>
public enum ActionTrustLevel
{
    /// <summary>プラットフォーム組み込み。</summary>
    Trusted,

    /// <summary>署名検証済みサードパーティ。</summary>
    Verified,

    /// <summary>コミュニティ Module（既定）。</summary>
    Community,

    /// <summary>未検証・低信頼。</summary>
    Untrusted,
}
