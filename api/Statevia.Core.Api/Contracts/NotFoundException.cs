namespace Statevia.Core.Api.Contracts;

/// <summary>
/// リソースが存在しないことを表す例外。
/// API は <see cref="ApiExceptionFilter"/> で契約の 404 に写像する。
/// </summary>
public sealed class NotFoundException : Exception
{
    /// <summary>
    /// <see cref="NotFoundException"/> を生成する。
    /// </summary>
    /// <param name="message">説明メッセージ。</param>
    public NotFoundException(string message) : base(message) { }
}
