namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>definitions と特定版を束ねた読み取りモデル。</summary>
public sealed class DefinitionDetail
{
    public required DefinitionRow Definition { get; init; }

    public required DefinitionVersionRow Version { get; init; }
}
