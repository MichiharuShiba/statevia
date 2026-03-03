namespace Statevia.CoreEngine.Application.Decide;

/// <summary>DecideResponse の拒否時エラー。architecture.v2 §4.1 / data-integration-contract §7。</summary>
public sealed record DecideError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, object?>? Details = null);

/// <summary>エラーコード。Core-API が HTTP に写像する。</summary>
public static class DecideErrorCodes
{
    public const string CommandRejected = "COMMAND_REJECTED";
    public const string InvalidInput = "INVALID_INPUT";
    public const string NotFound = "NOT_FOUND";
    public const string Internal = "INTERNAL";
}
