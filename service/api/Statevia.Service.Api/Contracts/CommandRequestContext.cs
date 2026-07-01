namespace Statevia.Service.Api.Contracts;

/// <summary>
/// 冪等コマンドの HTTP メタデータ（dedup キー組み立て用）。
/// </summary>
/// <param name="Method">HTTP メソッド。</param>
/// <param name="Path">リクエストパス（クエリなし）。</param>
public sealed record CommandRequestContext(string Method, string Path);
