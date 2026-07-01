using Statevia.Actions.Abstractions.Execution;

namespace Statevia.Core.Api.Application.Actions.Execution;

/// <summary>1 階層が課す完結した実行制約。</summary>
/// <remarks>
/// <para>緩和不可（base 下限を下げない）で合成される。各値は「下限の引き上げ」のみに作用する。</para>
/// <para>将来 <c>AllowedProviders</c> / <c>ResourceLimits</c> 等の多次元制約を追加できるよう record で定義する。</para>
/// </remarks>
/// <param name="MinimumMode">最低実行モード（隔離下限）。<c>null</c> は当該階層が下限を課さないことを表す。</param>
internal sealed record ExecutionPolicy(ActionExecutionMode? MinimumMode);
