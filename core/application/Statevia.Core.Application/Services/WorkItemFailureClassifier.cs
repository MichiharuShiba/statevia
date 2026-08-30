namespace Statevia.Core.Application.Services;

/// <summary>work item 処理の例外を恒久 / 一時に分ける。</summary>
/// <remarks>
/// <para>Worker の <c>catch</c> から呼び、判定表を HostedService に散らさない。</para>
/// <para>初期の恒久集合は Restore 不能と、受理済みなのに定義版行が無いこと。所有獲得失敗は一時。</para>
/// <para>実行時の Module 欠落は初期セットに入れない（一時障害として Release してよい）。</para>
/// </remarks>
internal static class WorkItemFailureClassifier
{
    /// <summary><see cref="ExecutionValidationMessages.StoredDefinitionVersionInvalid"/> と同じ安定文。</summary>
    internal const string StoredDefinitionVersionInvalidMessage =
        ExecutionValidationMessages.StoredDefinitionVersionInvalid;

    /// <summary>Restore 不能としてログに出す短い理由。</summary>
    internal const string ReasonRestoreInvalid = "restore_invalid";

    /// <summary>定義版行欠落としてログに出す短い理由。</summary>
    internal const string ReasonDefinitionVersionMissing = "definition_version_missing";

    /// <summary>試行上限到達としてログに出す短い理由。</summary>
    internal const string ReasonMaxAttempts = "max_attempts";

    /// <summary>所有獲得失敗としてログに出す短い理由。</summary>
    internal const string ReasonOwnershipMiss = "ownership_miss";

    /// <summary>
    /// Restore 不能（包み後の <see cref="StoredDefinitionVersionInvalidMessage"/>）または定義版行欠落か。
    /// </summary>
    /// <param name="exception">Worker / Lifecycle が捉えた例外。</param>
    /// <returns>同じ item を Release しても直らないとき <see langword="true"/>。</returns>
    public static bool IsPermanent(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (IsStoredDefinitionInvalid(current))
                return true;

            if (IsDefinitionVersionMissing(current))
                return true;
        }

        return false;
    }

    /// <summary>Restore 不能だけを見る（HTTP 422 写像用）。</summary>
    /// <param name="exception">保存版 Restore が投げた例外。</param>
    /// <returns>保存版が現行コンパイラで戻せないとき <see langword="true"/>。</returns>
    public static bool IsPermanentRestoreFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (IsStoredDefinitionInvalid(current))
                return true;
        }

        return false;
    }

    /// <summary>所有獲得失敗（<c>BeginOwnedSession</c> が null）。上限の対象外。</summary>
    /// <param name="generation">獲得した世代。失敗時は <see langword="null"/>。</param>
    /// <returns>所有を取れなかったとき <see langword="true"/>。</returns>
    public static bool IsOwnershipAcquisitionMiss(long? generation) => generation is null;

    /// <summary>例外から決まる恒久理由。未分類は <see langword="null"/>（上限到達は呼び出し側の attempts）。</summary>
    /// <param name="exception">判定した例外。</param>
    /// <returns>Restore 不能または定義版欠落の短い理由。それ以外は <see langword="null"/>。</returns>
    public static string? DescribeReason(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (IsPermanentRestoreFailure(exception))
            return ReasonRestoreInvalid;

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (IsDefinitionVersionMissing(current))
                return ReasonDefinitionVersionMissing;
        }

        return null;
    }

    private static bool IsDefinitionVersionMissing(Exception exception) =>
        exception is NotFoundException
        && string.Equals(
            exception.Message,
            ExecutionValidationMessages.DefinitionNotFound,
            StringComparison.Ordinal);

    private static bool IsStoredDefinitionInvalid(Exception exception) =>
        string.Equals(exception.Message, StoredDefinitionVersionInvalidMessage, StringComparison.Ordinal)
        && exception is InvalidOperationException or ApiValidationException;
}
