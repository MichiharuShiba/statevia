namespace Statevia.Core.Api.Services;

public interface IIdGenerator
{
    Guid NewGuid();
}

public sealed class UuidV7Generator : IIdGenerator
{
    public Guid NewGuid() => Guid.CreateVersion7();
}

