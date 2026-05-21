namespace Statevia.Core.Api.Persistence;

/// <summary>definitions と特定版を束ねた読み取りモデル。</summary>
internal sealed class DefinitionDetail
{
    public required DefinitionRow Definition { get; init; }

    public required DefinitionVersionRow Version { get; init; }
}
