namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>
/// Docker ベースの Container サンドボックス設定。
/// セクション: <c>Statevia:ExecutionPolicy:Sandbox:Docker</c>。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ActionRuntimeProfile"/> は RuntimeSpace の <c>RuntimeId</c> とは別概念。
/// v1 は <c>dotnet-8.0</c> のみ許可し、複数プロファイル解決は後続。
/// </para>
/// <para>セキュリティ: Endpoint 認証情報はログへ出さない。</para>
/// </remarks>
internal sealed class DockerSandboxOptions
{
    /// <summary>v1 で許可する既定 ActionRuntimeProfile。</summary>
    public const string DefaultActionRuntimeProfile = "dotnet-8.0";

    /// <summary>コンテナ内 modules マウント先の既定（Compose action-host と揃える）。</summary>
    public const string DefaultModulesContainerPath = "/app/modules";

    /// <summary>コンテナ内 gRPC 待受ポートの既定。</summary>
    public const int DefaultGrpcPort = 5001;

    /// <summary>
    /// <see cref="DefaultTimeoutSeconds"/> の下限（10 秒）。
    /// コールドスタート〜 Echo 完了の実測安定床（5 秒）に余裕を載せた運用下限。
    /// </summary>
    public const int MinDefaultTimeoutSeconds = 10;

    /// <summary>
    /// <see cref="DefaultTimeoutSeconds"/> の上限（1 時間）。
    /// 共有ホストでの長時間占有を抑え、かつ <c>CancelAfter</c> の過大値を起動時に防ぐ。
    /// </summary>
    public const int MaxDefaultTimeoutSeconds = 3_600;

    /// <summary>
    /// <see cref="GrpcPort"/> の下限。
    /// well-known ポート帯（1〜1023）への誤設定を避け、非特権ポートのみ許可する。
    /// </summary>
    public const int MinGrpcPort = 1_024;

    /// <summary><see cref="GrpcPort"/> の上限。</summary>
    public const int MaxGrpcPort = 65_535;

    /// <summary>Docker デーモン Endpoint（未設定時は OS 既定ソケット）。</summary>
    public string? Endpoint { get; set; }

    /// <summary>Action 実行環境プロファイル（既定 <c>dotnet-8.0</c>）。</summary>
    public string ActionRuntimeProfile { get; set; } = DefaultActionRuntimeProfile;

    /// <summary>起動するコンテナイメージ（Action Host 相当）。未設定時は実行不可。</summary>
    public string? Image { get; set; }

    /// <summary>
    /// Docker NetworkMode。既定 <c>bridge</c>（ホストからの gRPC 到達用）。
    /// <c>none</c> は v1 非対応。
    /// </summary>
    public string NetworkMode { get; set; } = "bridge";

    /// <summary>ホスト側 modules ルート（コンテナへ bind-mount）。未設定時はマウントしない。</summary>
    public string? ModulesHostPath { get; set; }

    /// <summary>コンテナ内の modules マウント先。</summary>
    public string ModulesContainerPath { get; set; } = DefaultModulesContainerPath;

    /// <summary>コンテナ内 gRPC 待受ポート。</summary>
    public int GrpcPort { get; set; } = DefaultGrpcPort;

    /// <summary>
    /// タイムアウト未指定時の既定秒数。
    /// 無制限実行を避けるための安全側既定。
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 60;
}
