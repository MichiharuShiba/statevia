namespace Statevia.Core.Api.Application.Actions.Execution;

/// <summary>スコープと、そのスコープが課す <see cref="ExecutionPolicy"/> の組。</summary>
/// <param name="Scope">適用階層。</param>
/// <param name="Policy">当該階層の制約。</param>
internal sealed record ScopedExecutionPolicy(ExecutionPolicyScope Scope, ExecutionPolicy Policy);
