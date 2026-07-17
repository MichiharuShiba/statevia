using Grpc.Net.Client;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Infrastructure.Actions.Grpc.Contracts;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>一時 BaseUrl 向け Action Host gRPC 実行の差し替え点。</summary>
/// <remarks>単体テストでは fake を注入し、実 gRPC を避ける。</remarks>
internal interface IEphemeralActionHostExecutor
{
    /// <summary>指定 BaseUrl の Action Host へ実行を委譲する。</summary>
    /// <param name="baseUrl">コンテナ到達 URL。</param>
    /// <param name="request">実行リクエスト。</param>
    /// <param name="cancellationToken">キャンセル（タイムアウト含む）。</param>
    /// <returns>実行結果。</returns>
    Task<ActionExecutionResult> ExecuteAsync(
        string baseUrl,
        ActionExecutionRequest request,
        CancellationToken cancellationToken);
}

/// <summary>短命コンテナ向けに都度 <see cref="GrpcChannel"/> を開く <see cref="IEphemeralActionHostExecutor"/>。</summary>
/// <param name="httpClient">テスト用の共有 <see cref="HttpClient"/>。省略時は実行ごとに生成する。</param>
internal sealed class GrpcEphemeralActionHostExecutor(HttpClient? httpClient = null) : IEphemeralActionHostExecutor
{
    /// <inheritdoc />
    public async Task<ActionExecutionResult> ExecuteAsync(
        string baseUrl,
        ActionExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentNullException.ThrowIfNull(request);

        var address = baseUrl.Trim();
        // 短命コンテナは TLS 無し（h2c）。.NET は既定で平文 HTTP/2 を拒否するため明示的に許可する。
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        using var ownedHandler = httpClient is null
            ? new SocketsHttpHandler { EnableMultipleHttp2Connections = true }
            : null;
        using var ownedClient = httpClient is null
            ? new HttpClient(ownedHandler!) { BaseAddress = new Uri(address) }
            : null;
        var channelHttpClient = httpClient ?? ownedClient!;
        using var channel = GrpcChannel.ForAddress(
            address,
            new GrpcChannelOptions { HttpClient = channelHttpClient });
        var client = new ActionExecutionService.ActionExecutionServiceClient(channel);
        return await ActionHostGrpcInvoker.ExecuteAsync(client, request, cancellationToken).ConfigureAwait(false);
    }
}
