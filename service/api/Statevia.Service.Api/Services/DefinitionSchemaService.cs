using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Application.Definition;

namespace Statevia.Service.Api.Services;

/// <summary>
/// 現行の nodes スキーマ供給実装。
/// 将来は DTO からの自動生成実装へ差し替える想定。
/// </summary>
internal sealed class DefinitionSchemaService : IDefinitionSchemaService
{
    public object GetNodesSchemaDocument() => NodesSchemaDefinition.CreateSchemaDocument();

    public string GetNodesSchemaVersion() => NodesSchemaDefinition.SchemaVersion;

    public int GetNodesVersion() => NodesSchemaDefinition.NodesVersion;
}
