namespace Statevia.Runtime.Configuration;

/// <summary>ランタイム処理を API プロセス内で有効化するかを制御する。</summary>
public sealed class RuntimeOptions
{
    /// <summary>設定セクション名。</summary>
    public const string SectionName = "Statevia:Runtime";

    /// <summary>API 内ワーカーを有効化するか。</summary>
    public bool EnableInProcessWorker { get; set; } = true;

    /// <summary>API 内 DelayWait スケジューラーを有効化するか。</summary>
    public bool EnableInProcessDelayWaitScheduler { get; set; } = true;

    /// <summary>API 内所有権回復スケジューラーを有効化するか。</summary>
    public bool EnableInProcessOwnershipRecovery { get; set; } = true;
}
