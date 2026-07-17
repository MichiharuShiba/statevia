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
