namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// <c>clientEventId</c> 付きの Engine 操作の結果。同一キーの再適用時は <see cref="AlreadyApplied"/>（No-Op）。
/// </summary>
public readonly struct ApplyResult : IEquatable<ApplyResult>
{
    private readonly bool _alreadyApplied;

    private ApplyResult(bool alreadyApplied) => _alreadyApplied = alreadyApplied;

    /// <summary>初回の適用（イベント発行またはキャンセル）が行われたことを示す。</summary>
    public bool IsApplied => !_alreadyApplied;

    /// <summary>同一 <c>clientEventId</c> が既に処理済みで副作用がなかった場合は true。</summary>
    public bool IsAlreadyApplied => _alreadyApplied;

    /// <summary>新規適用。</summary>
    public static ApplyResult Applied => new(false);

    /// <summary>重複キーによる No-Op。</summary>
    public static ApplyResult AlreadyApplied => new(true);

    /// <inheritdoc />
    public bool Equals(ApplyResult other) => _alreadyApplied == other._alreadyApplied;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ApplyResult other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _alreadyApplied ? 1 : 0;

    /// <summary>等価演算子。</summary>
    public static bool operator ==(ApplyResult left, ApplyResult right) => left.Equals(right);

    /// <summary>非等価演算子。</summary>
    public static bool operator !=(ApplyResult left, ApplyResult right) => !left.Equals(right);
}
