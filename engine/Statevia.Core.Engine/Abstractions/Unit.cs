namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// 意味のある出力を持たない状態用の Unit 型。
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>単一の既定値。</summary>
    public static readonly Unit Value = default;

    /// <inheritdoc />
    public bool Equals(Unit other) => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <inheritdoc />
    public override string ToString() => "()";

    /// <summary>2 つの <see cref="Unit"/> が等しいかどうかを返す（常に true）。</summary>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>2 つの <see cref="Unit"/> が異なるかどうかを返す（常に false）。</summary>
    public static bool operator !=(Unit left, Unit right) => false;
}
