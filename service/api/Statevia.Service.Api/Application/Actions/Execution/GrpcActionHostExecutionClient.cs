using Grpc.Net.Client;
using Microsoft.Extensions.Options;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Infrastructure.Actions.Grpc.Contracts;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>gRPC 経由の Action Host 実行クライアント。</summary>
internal sealed class GrpcActionHostExecutionClient : IActionHostExecutionClient, IDisposable
{
    private readonly GrpcChannel? _channel;
    private readonly ActionExecutionService.ActionExecutionServiceClient? _client;

    /// <summary>gRPC クライアントを構築する。</summary>
    /// <param name="options">Action Host 接続設定。</param>
    public GrpcActionHostExecutionClient(IOptions<ActionHostClientOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var actionHostOptions = options.Value;

        if (string.IsNullOrWhiteSpace(actionHostOptions.BaseUrl))
        {
            return;
        }

        _channel = GrpcChannel.ForAddress(actionHostOptions.BaseUrl.Trim());
        _client = new ActionExecutionService.ActionExecutionServiceClient(_channel);
    }

    /// <inheritdoc />
    public Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            return Task.FromResult(new ActionExecutionResult
            {
                Success = false,
                ErrorCode = "ActionHostNotConfigured",
                ErrorMessage = "Action Host base URL is not configured.",
            });
        }

        return ActionHostGrpcInvoker.ExecuteAsync(_client, request, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose() => _channel?.Dispose();
}
