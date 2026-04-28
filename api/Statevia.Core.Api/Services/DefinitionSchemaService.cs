using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Application.Definition;

namespace Statevia.Core.Api.Services;

/// <summary>
/// 現行の nodes スキーマ供給実装。
/// 将来は DTO からの自動生成実装へ差し替える想定。
/// </summary>
public sealed class DefinitionSchemaService : IDefinitionSchemaService
{
    public object GetNodesSchemaDocument() => NodesSchemaDefinition.CreateSchemaDocument();

    public string GetNodesSchemaVersion() => NodesSchemaDefinition.SchemaVersion;

    public int GetNodesVersion() => NodesSchemaDefinition.NodesVersion;
}
