namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>
/// 定義検証の 1 件。安定 <c>code</c>（<see cref="RuleId"/>）とメッセージを持つ。
/// </summary>
/// <param name="RuleId">ルール識別子。HTTP / CLI の <c>code</c> と一致させる。</param>
/// <param name="Message">人向けメッセージ（既存 <see cref="ValidationResult.Errors"/> と互換）。</param>
/// <param name="StateName">対象状態名。ワークフロー全体の指摘では null。</param>
public sealed record ValidationIssue(string RuleId, string Message, string? StateName = null);
