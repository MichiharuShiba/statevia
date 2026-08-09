namespace Statevia.Core.Application.Contracts.Services;

/// <summary>
/// 分散環境で衝突しにくい ID 生成の契約。
/// </summary>
public interface IIdGenerator
{
    /// <summary>永続 PK / 文書キー向けの UUID（実装は UUIDv7・時刻順）。</summary>
    /// <returns>生成された GUID。</returns>
    Guid NewSequentialGuid();

    /// <summary>非永続 ID 向けの乱数 UUID（実装は UUIDv4 相当）。</summary>
    /// <returns>生成された GUID。</returns>
    Guid NewRandomGuid();
}
