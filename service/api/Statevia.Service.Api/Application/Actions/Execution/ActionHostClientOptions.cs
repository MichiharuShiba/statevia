namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>Core-API から Action Host gRPC クライアントへ接続する設定。</summary>
internal sealed class ActionHostClientOptions
{
    /// <summary>設定セクション名（Action Host サーバーと共有）。</summary>
    public const string SectionName = "Statevia:ActionHost";

    /// <summary>Action Host のベース URL（例: <c>http://localhost:5001</c>）。</summary>
    public string? BaseUrl { get; set; }
}
