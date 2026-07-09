namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary><see cref="IDefinitionRepository.SoftDeleteAsync"/> の結果。</summary>
public enum DefinitionSoftDeleteOutcome
{
    /// <summary>定義が存在しない、またはテナント／project 境界外。</summary>
    NotFound,

    /// <summary>新たに論理削除した。</summary>
    Deleted,

    /// <summary>既に削除済み（冪等成功）。</summary>
    AlreadyDeleted,
}
