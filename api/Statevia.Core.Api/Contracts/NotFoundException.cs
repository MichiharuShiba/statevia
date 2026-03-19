namespace Statevia.Core.Api.Contracts;

/// <summary>
/// リソースが存在しないことを表す例外。
/// API は ApiExceptionFilter で契約の 404 に写像する。
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

