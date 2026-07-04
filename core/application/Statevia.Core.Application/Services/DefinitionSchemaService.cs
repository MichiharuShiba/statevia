namespace Statevia.Core.Application.Services;

/// <summary>
/// 現行の nodes スキーマ供給実装。
/// 将来は DTO からの自動生成実装へ差し替える想定。
/// </summary>
internal sealed class DefinitionSchemaService : IDefinitionSchemaService
{
    private readonly INodesSchemaProvider _schemaProvider;

    public DefinitionSchemaService(INodesSchemaProvider schemaProvider)
    {
        _schemaProvider = schemaProvider;
    }

    public object GetNodesSchemaDocument() => _schemaProvider.GetSchemaDocument();

    public string GetNodesSchemaVersion() => _schemaProvider.GetSchemaVersion();

    public int GetNodesVersion() => _schemaProvider.GetNodesVersion();
}
