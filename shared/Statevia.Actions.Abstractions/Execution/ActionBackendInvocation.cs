using Statevia.Actions.Abstractions.Catalog;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Actions.Abstractions.Execution;

/// <summary>各 Backend が必要分のみ参照する、統一された Action 実行呼び出し DTO。</summary>
/// <remarks>
/// <para>InProcess は <see cref="Registration"/> / <see cref="StateContext"/> を使用する。</para>
/// <para>OutOfProcess 等は <see cref="Request"/> / <see cref="RuntimeInput"/> のみ使用し、Engine 内部状態は受け取らない。</para>
/// </remarks>
/// <param name="Request">Platform 実行リクエスト。</param>
/// <param name="RuntimeInput">Engine が解決した状態入力。</param>
/// <param name="Registration">Catalog 登録情報（InProcess 実行で使用。他 Mode では <c>null</c> 可）。</param>
/// <param name="StateContext">Engine 状態コンテキスト（InProcess 実行で使用。他 Mode では <c>null</c> 可）。</param>
public sealed record ActionBackendInvocation(
    ActionExecutionRequest Request,
    object? RuntimeInput,
    ActionRegistration? Registration = null,
    StateContext? StateContext = null);
