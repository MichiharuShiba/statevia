namespace Statevia.CoreEngine.Application.Decide;

/// <summary>DecideRequest.actor。JSON では kind が小文字の文字列（"user" 等）。</summary>
public sealed record ActorDto(string Kind, string? Id = null);
