namespace Statevia.Service.Api.Application.Definition;

/// <summary>
/// nodes 形式（UI スキーマ・<see cref="NodesWorkflowDefinitionLoader"/> 共通）の JSON キー・型名・列挙値。
/// </summary>
internal static class NodesSchemaLiteral
{
    public const string TypeObject = "object";

    public const string TypeString = "string";

    public const string TypeInteger = "integer";

    public const string TypeArray = "array";

    public const string TypeBoolean = "boolean";

    public const string TypeNumber = "number";

    public const string TypeNull = "null";

    public const string KeyVersion = "version";

    public const string KeyWorkflow = "workflow";

    public const string KeyNodes = "nodes";

    public const string KeyControls = "controls";

    public const string KeyId = "id";

    public const string KeyType = "type";

    public const string KeyName = "name";

    public const string KeyNext = "next";

    public const string KeyError = "error";

    public const string KeyAction = "action";

    public const string KeyEvent = "event";

    public const string KeyBranches = "branches";

    public const string KeyInput = "input";

    public const string KeyOutput = "output";

    public const string KeyMode = "mode";

    public const string KeyEdges = "edges";

    public const string KeyTo = "to";

    public const string KeyWhen = "when";

    public const string KeyPath = "path";

    public const string KeyOp = "op";

    public const string KeyOrder = "order";

    public const string KeyDefault = "default";

    /// <summary>検証メッセージ用の論理フィールドパス（<c>edges[].to</c>）。</summary>
    public const string KeyEdgesTo = "edges.to";

    public const string NodeTypeStart = "start";

    public const string NodeTypeEnd = "end";

    public const string NodeTypeAction = "action";

    public const string NodeTypeWait = "wait";

    public const string NodeTypeFork = "fork";

    public const string NodeTypeJoin = "join";

    public const string JoinModeAll = "all";
}
