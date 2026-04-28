namespace Statevia.Core.Api.Abstractions.Services;

/// <summary>
/// Definition 入力スキーマの供給サービス。
/// 将来 DTO 起点のスキーマ生成へ差し替える境界として利用する。
/// </summary>
public interface IDefinitionSchemaService
{
    object GetNodesSchemaDocument();
    string GetNodesSchemaVersion();
    int GetNodesVersion();
}
