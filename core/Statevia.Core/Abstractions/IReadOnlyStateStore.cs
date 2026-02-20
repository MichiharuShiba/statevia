namespace Statevia.Core.Abstractions;

/// <summary>
/// 完了済み状態の出力を読み取り専用で参照するストア。
/// Join 状態が依存状態の出力を取得する際に使用します。
/// </summary>
public interface IReadOnlyStateStore
{
    /// <summary>指定した状態の出力を取得します。未完了の場合は false を返します。</summary>
    bool TryGetOutput(string stateName, out object? output);
}
