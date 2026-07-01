namespace Statevia.Service.Api.Abstractions.Services;

/// <summary>
/// 分散環境で衝突しにくい ID 生成の契約。
/// </summary>
public interface IIdGenerator
{
    /// <summary>新しい GUID（実装は UUID v7 等）を生成する。</summary>
    /// <returns>生成された GUID。</returns>
    Guid NewGuid();
}
