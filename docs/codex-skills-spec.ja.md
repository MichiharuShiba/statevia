# Codex Skills 仕様（実行型ステートマシン）

Version: 1.0

## 1. 目的

Codexが本リポジトリで一貫して正しい判断をするために、
プロジェクト固有のルールを Skills として共有する。

## 2. 必須スキル（推奨セット）

### Skill: cancel-wins

- 目的：Cancel wins を全実装の最優先に固定
- 内容：
  - ExecutionStatus / NodeStatus の優先順位（CANCELED最強）
  - cancelRequestedAt がある場合の進行系コマンド拒否（409）をデフォルト
  - reducer は rank で担保し、散発的if増殖を避ける

### Skill: docs-first-core

- 目的：コア変更は docs の更新を先に行う
- 内容：
  - 仕様（docs）→ 実装 → テスト → PR要約 の順
  - 変更は小さく、検証可能に

### Skill: ui-visual-rules

- 目的：UIでの強弱（Cancel/Fail/Wait/Running）を統一
- 内容：
  - Cancelが最も強い
  - Wait/Resume/Cancel は視線誘導
  - Failed/Cancel は定義増でも一目で判別
  - Runningは控えめ
  - Fork/Joinはグルーピングでまとまり感

## 3. スキルの適用範囲

- コア：常時適用（cancel-wins / docs-first-core）
- UI：ui-visual-rules を適用
- 例外：実験ブランチのみ（例外時はプロンプトに明記）
