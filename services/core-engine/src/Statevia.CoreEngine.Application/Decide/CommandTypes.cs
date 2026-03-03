namespace Statevia.CoreEngine.Application.Decide;

/// <summary>DecideRequest.command.type の固定値。core-api のコマンド種別に合わせる。</summary>
public static class CommandTypes
{
    public const string CreateExecution = "CreateExecution";
    public const string StartExecution = "StartExecution";
    public const string CancelExecution = "CancelExecution";
    public const string CreateNode = "CreateNode";
    public const string StartNode = "StartNode";
    public const string PutNodeWaiting = "PutNodeWaiting";
    public const string ResumeNode = "ResumeNode";
}
