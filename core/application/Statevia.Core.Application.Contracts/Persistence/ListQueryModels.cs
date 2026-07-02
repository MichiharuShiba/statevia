namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>ページング（offset / limit）。</summary>
public sealed record PageQuery(
    int Offset,
    int Limit);

/// <summary>ソート指定。</summary>
public sealed record SortQuery(
    string? SortBy,
    string? SortOrder);

/// <summary>定義一覧のページクエリ。</summary>
public sealed record DefinitionListPageQuery(
    PageQuery Page,
    SortQuery Sort,
    string? NameContains);

/// <summary>実行一覧のページクエリ。</summary>
public sealed record ExecutionListPageQuery(
    PageQuery Page,
    SortQuery Sort,
    string? StatusFilter,
    Guid? DefinitionIdFilter,
    string? NameContains);
