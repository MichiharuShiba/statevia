namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>
/// 定義検証ルールの実行フェーズ。後続フェーズは先行フェーズが成功したときだけ走る。
/// </summary>
public enum ValidationPhase
{
    /// <summary>状態名・参照・遷移形状など（旧 Level1）。</summary>
    Structural = 0,

    /// <summary>到達不能・循環 Join（旧 Level2）。</summary>
    Reachability = 1,

    /// <summary>Fork 領域・Join 供給・枝の独立 Definition。</summary>
    ForkRegion = 2
}
