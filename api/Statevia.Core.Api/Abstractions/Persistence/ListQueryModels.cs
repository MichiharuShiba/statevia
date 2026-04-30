using System;

namespace Statevia.Core.Api.Abstractions.Persistence;

public sealed record PageQuery(
    int Offset,
    int Limit);

public sealed record SortQuery(
    string? SortBy,
    string? SortOrder);

public sealed record DefinitionListPageQuery(
    PageQuery Page,
    SortQuery Sort,
    string? NameContains);

public sealed record WorkflowListPageQuery(
    PageQuery Page,
    SortQuery Sort,
    string? StatusFilter,
    Guid? DefinitionIdFilter,
    string? NameContains);
