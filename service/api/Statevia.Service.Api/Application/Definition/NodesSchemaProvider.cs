namespace Statevia.Service.Api.Application.Definition;

/// <summary>
/// <see cref="INodesSchemaProvider"/> 実装。<see cref="NodesSchemaDefinition"/> の静的メソッドに委譲する。
/// </summary>
internal sealed class NodesSchemaProvider : INodesSchemaProvider
{
    public object GetSchemaDocument() => NodesSchemaDefinition.CreateSchemaDocument();

    public string GetSchemaVersion() => NodesSchemaDefinition.SchemaVersion;

    public int GetNodesVersion() => NodesSchemaDefinition.NodesVersion;
}
