namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>Docker デーモン操作の境界（SDK 非依存の契約）。</summary>
/// <remarks>
/// Create / Start / ポート解決 / Stop+Remove を担う。
/// Docker.DotNet 等の型はこのインタフェースの実装に封じる。
/// </remarks>
internal interface IDockerContainerClient
{
    /// <summary>Action Host 相当コンテナを起動し、ホストから到達可能な BaseUrl を返す。</summary>
    /// <param name="request">起動パラメータ。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>起動済みコンテナ情報。</returns>
    /// <exception cref="InvalidOperationException">デーモン不通・起動失敗など。</exception>
    Task<DockerStartedContainer> StartActionHostContainerAsync(
        DockerContainerStartRequest request,
        CancellationToken cancellationToken);

    /// <summary>コンテナを停止して削除する（失敗しても例外を握りつぶさない実装側方針可）。</summary>
    /// <param name="containerId">コンテナ ID。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    Task StopAndRemoveAsync(string containerId, CancellationToken cancellationToken);
}

/// <summary>短命 Action Host コンテナの起動要求。</summary>
/// <param name="Image">コンテナイメージ。</param>
/// <param name="NetworkMode">Docker NetworkMode（例: bridge）。</param>
/// <param name="CpuLimit">CPU 上限（コア数換算）。null は未指定。</param>
/// <param name="MemoryLimitMiB">メモリ上限 MiB。null は未指定。</param>
/// <param name="ModulesHostPath">ホスト modules ルート。null/空なら bind-mount しない。</param>
/// <param name="ModulesContainerPath">コンテナ内マウント先。</param>
/// <param name="ContainerGrpcPort">コンテナ内 gRPC ポート。</param>
internal sealed record DockerContainerStartRequest(
    string Image,
    string NetworkMode,
    double? CpuLimit,
    int? MemoryLimitMiB,
    string? ModulesHostPath,
    string ModulesContainerPath,
    int ContainerGrpcPort);

/// <summary>起動済み短命コンテナ。</summary>
/// <param name="ContainerId">Docker コンテナ ID。</param>
/// <param name="BaseUrl">ホストから gRPC 接続する BaseUrl（例: http://127.0.0.1:49152）。</param>
internal sealed record DockerStartedContainer(string ContainerId, string BaseUrl);
