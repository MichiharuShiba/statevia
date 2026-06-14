namespace Statevia.Actions.Abstractions.Publication;

/// <summary>Statevia JSON Schema 拡張 vocabulary のキー名と valueKind 定数。</summary>
public static class StateviaActionSchemaVocabulary
{
    /// <summary>フィールド値の種別（literal / path / literalOrPath）。</summary>
    public const string ValueKindKeyword = "x-statevia-valueKind";

    /// <summary>UI ヒント（登録時に ActionUiMetadata へ正規化）。</summary>
    public const string UiKeyword = "x-statevia-ui";

    /// <summary>将来の Expression 型参照（Phase 9）。</summary>
    public const string ExpressionKeyword = "x-statevia-expression";

    /// <summary>JSON Schema Draft 2020-12。</summary>
    public const string DefaultSchemaVersion = "2020-12";

    /// <summary>リテラル値のみ許可。</summary>
    public const string ValueKindLiteral = "literal";

    /// <summary>SimpleJsonPath（<c>$.…</c>）のみ許可。</summary>
    public const string ValueKindPath = "path";

    /// <summary>リテラルまたは path（デフォルト）。</summary>
    public const string ValueKindLiteralOrPath = "literalOrPath";
}
