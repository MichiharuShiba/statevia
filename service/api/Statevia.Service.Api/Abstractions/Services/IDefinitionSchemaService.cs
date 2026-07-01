namespace Statevia.Service.Api.Abstractions.Services;

/// <summary>
/// Definition 入力スキーマの供給サービス。
/// 将来 DTO 起点のスキーマ生成へ差し替える境界として利用する。
/// </summary>
public interface IDefinitionSchemaService
{
    /// <summary>nodes 形式の JSON Schema 相当のドキュメントを返す。</summary>
    /// <returns>スキーマオブジェクト（JSON シリアライズ可能）。</returns>
    object GetNodesSchemaDocument();

    /// <summary>スキーマ文書のバージョン文字列を返す。</summary>
    string GetNodesSchemaVersion();

    /// <summary>nodes スキーマの整数バージョンを返す。</summary>
    int GetNodesVersion();
}
