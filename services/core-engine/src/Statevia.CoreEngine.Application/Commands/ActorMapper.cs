using Statevia.CoreEngine.Application.Decide;
using Statevia.CoreEngine.Domain.Events;

namespace Statevia.CoreEngine.Application.Commands;

/// <summary>ActorDto（JSON 小文字 kind）を Domain Actor にマップする。</summary>
public static class ActorMapper
{
    public static Actor ToDomain(this ActorDto dto)
    {
        var kind = dto.Kind?.ToLowerInvariant() switch
        {
            "system" => ActorKind.System,
            "user" => ActorKind.User,
            "scheduler" => ActorKind.Scheduler,
            "external" => ActorKind.External,
            _ => ActorKind.System,
        };
        return new Actor(kind, dto.Id);
    }
}
