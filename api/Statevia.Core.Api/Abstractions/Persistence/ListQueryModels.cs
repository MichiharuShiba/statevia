using System;

namespace Statevia.Core.Api.Abstractions.Persistence;

internal sealed record PageQuery(
    int Offset,
    int Limit);

internal sealed record SortQuery(
    string? SortBy,
    string? SortOrder);

internal sealed record DefinitionListPageQuery(
    PageQuery Page,
    SortQuery Sort,
    string? NameContains);

internal sealed record ExecutionListPageQuery(
    PageQuery Page,
    SortQuery Sort,
    string? StatusFilter,
    Guid? DefinitionIdFilter,
    string? NameContains);
