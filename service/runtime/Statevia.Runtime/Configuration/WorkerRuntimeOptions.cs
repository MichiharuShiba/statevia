namespace Statevia.Runtime.Configuration;

/// <summary>ワーカーのプロセス内並列と無進捗 watchdog、試行上限の起動設定。</summary>
/// <remarks>
/// <para>セクション <c>Statevia:Runtime:Worker</c>。API 内 HostedService と split-runtime の worker で同じキーを読む。</para>
/// <para>範囲外は <c>ValidateOnStart</c> で起動失敗する。既定へ黙って落とさない。</para>
/// <para>既定 <see cref="MaxConcurrency"/> は 1（現行の直列互換）。上限 64 は暴発防止であり容量の推奨ではない。</para>
/// <para>既定 <see cref="MaxAttempts"/> は 20。所有獲得失敗はカウントしない。</para>
/// </remarks>
public sealed class WorkerRuntimeOptions
{
    /// <summary>設定セクション名。</summary>
    public const string SectionName = "Statevia:Runtime:Worker";

    /// <summary>ライフサイクル同時処理の下限（現行互換の 1）。</summary>
    public const int MinMaxConcurrency = 1;

    /// <summary>ライフサイクル同時処理の上限。暴発防止であり推奨値ではない。</summary>
    public const int MaxMaxConcurrency = 64;

    /// <summary>Cancel 制御ループ同時処理の下限。</summary>
    public const int MinCancelConcurrency = 1;

    /// <summary>Cancel は短時間のため大きな値は不要。</summary>
    public const int MaxCancelConcurrency = 8;

    /// <summary>無進捗判定の最短。これより短いと遅い persist を誤検知する。</summary>
    public static readonly TimeSpan MinNoProgressTimeout = TimeSpan.FromSeconds(30);

    /// <summary>無進捗判定の最長。これより長いとスロット占有が続く。</summary>
    public static readonly TimeSpan MaxNoProgressTimeout = TimeSpan.FromHours(24);

    /// <summary>未分類例外の再試行下限。0 は無限 Release になるので拒否する。</summary>
    public const int MinMaxAttempts = 1;

    /// <summary>未分類例外の再試行上限。暴発防止であり推奨値ではない。</summary>
    public const int MaxMaxAttempts = 1000;

    /// <summary>Start / Resume のプロセス内同時処理件数。既定 1。</summary>
    public int MaxConcurrency { get; set; } = MinMaxConcurrency;

    /// <summary>Cancel 制御ループの同時処理件数。既定 1。</summary>
    public int CancelConcurrency { get; set; } = MinCancelConcurrency;

    /// <summary>
    /// 非 Wait Running が無い無進捗がこの時間を超えたら Unload する。既定 10 分。
    /// </summary>
    public TimeSpan NoProgressTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Start / Resume / Cancel の claim 試行上限。既定 20。所有獲得失敗は対象外。
    /// </summary>
    public int MaxAttempts { get; set; } = 20;
}
