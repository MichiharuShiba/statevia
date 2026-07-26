namespace Statevia.Core.Engine.Definition;

/// <summary>
/// SimpleJsonPath のセグメント種別。
/// </summary>
/// <remarks>
/// <para><see cref="Identifier"/> / <see cref="QuotedKey"/> はオブジェクトキー参照。</para>
/// <para><see cref="ArrayIndex"/> は配列の 0 始まりインデックス参照（読み取り専用）。</para>
/// </remarks>
internal enum PathSegmentKind
{
    /// <summary>ドット区切り識別子（例: <c>.users</c>）。</summary>
    Identifier,

    /// <summary>ブラケット＋引用キー（例: <c>['order.notify']</c>）。</summary>
    QuotedKey,

    /// <summary>ブラケット＋非負整数インデックス（例: <c>[0]</c>）。</summary>
    ArrayIndex,
}

/// <summary>
/// SimpleJsonPath の 1 セグメント（識別子・引用キー・配列インデックス）。
/// </summary>
/// <remarks>
/// <para><see cref="Kind"/> が <see cref="PathSegmentKind.Identifier"/> / <see cref="PathSegmentKind.QuotedKey"/> のとき <see cref="Name"/> が非 null。</para>
/// <para><see cref="Kind"/> が <see cref="PathSegmentKind.ArrayIndex"/> のとき <see cref="Index"/> が非 null（0 以上）。</para>
/// </remarks>
/// <param name="Kind">セグメント種別。</param>
/// <param name="Name">識別子または引用キー。ArrayIndex のときは null。</param>
/// <param name="Index">配列インデックス。Identifier / QuotedKey のときは null。</param>
internal readonly record struct PathSegment(PathSegmentKind Kind, string? Name, int? Index)
{
    /// <summary>識別子セグメントを生成する。</summary>
    /// <param name="name">識別子。</param>
    /// <returns>Identifier セグメント。</returns>
    public static PathSegment ForIdentifier(string name) =>
        new(PathSegmentKind.Identifier, name, Index: null);

    /// <summary>引用キーセグメントを生成する。</summary>
    /// <param name="name">キー文字列（引用を除いた値）。</param>
    /// <returns>QuotedKey セグメント。</returns>
    public static PathSegment ForQuotedKey(string name) =>
        new(PathSegmentKind.QuotedKey, name, Index: null);

    /// <summary>配列インデックスセグメントを生成する。</summary>
    /// <param name="index">0 以上のインデックス。</param>
    /// <returns>ArrayIndex セグメント。</returns>
    public static PathSegment ForArrayIndex(int index) =>
        new(PathSegmentKind.ArrayIndex, Name: null, index);
}
