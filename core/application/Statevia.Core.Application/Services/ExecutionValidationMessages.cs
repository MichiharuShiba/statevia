namespace Statevia.Core.Application.Services;

/// <summary>
/// Execution 関連の安定失敗メッセージ。
/// </summary>
internal static class ExecutionValidationMessages
{
    public const string DefinitionNotFound = "Definition not found";

    public const string ExecutionNotFound = "Execution not found";

    /// <summary>保存版を現行コンパイラで Restore できないときの安定メッセージ。</summary>
    public const string StoredDefinitionVersionInvalid = "Stored definition version is invalid.";

    /// <summary>親実行に security snapshot が無く子開始できない。</summary>
    public const string ParentSecuritySnapshotRequired = "Parent execution security snapshot is required.";

    /// <summary>子 definitionId が現在または祖先定義と一致する。</summary>
    public const string ChildWorkflowSelfReference = "Child workflow cannot start the current or ancestor definition.";
}
