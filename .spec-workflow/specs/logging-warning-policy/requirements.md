# Requirements: Warning ポリシー（STV-405 / LOG-3）

## Introduction

Engine 実行において、**継続可能だが契約上注意が必要な状況**を `LogWarning` で一貫出力する。対象は `v2-logging-v1-tasks.md` の **input 評価の注意**および**遷移なしで停止**（FSM が次遷移を返さない等）を想定する。

**紐づくチケット**: `STV-405`（`v2-ticket-backlog.md`）、`LOG-3`（`v2-logging-v1-tasks.md`）。

**依存**: `STV-404`（Engine 実行ログの基盤）。

## Alignment with Product Vision

異常と警告の区別は運用ノイズと対応優先度に直結する。Warning は「止まらないが調査が必要な」シグナルとして明文化する。

## Requirements

### Requirement 1 — input 評価の注意

**User Story:** As a **運用者**, I want **StateInput 評価でフォールバックや型注意が発生したとき Warning ログに残る**こと, so that **入力データ品質を追跡できる**。

#### Acceptance Criteria — Requirement 1

1. WHEN **`StateInputEvaluator`（または同等）が「注意が必要な」解釈を行う** THEN **システムは `workflowId`, `stateName`, `inputKey`, `reason` を含む Warning ログを出す**（項目名は `STV-407` で統一可能。本 spec では v1 表に整合）。
2. IF **注意条件が複数ある** THEN **ログは重複しすぎないよう design で抑制方針を定める**（同一 state 同一入力ハッシュで 1 回など）。

### Requirement 2 — 遷移なし停止

**User Story:** As a **運用者**, I want **FSM 評価の結果、次の遷移がなく実行が止まる場合に Warning が出る**こと, so that **デッドロックに近い停止を検知できる**。

#### Acceptance Criteria — Requirement 2

1. WHEN **`Evaluate` の結果が「遷移なし」かつ終端でもない** THEN **システムは `workflowId`, `stateName`, `fact` を含む Warning ログを出す**。
2. WHEN **上記 Warning が出る** THEN **実行は既存セマンティクスを変えない**（ログ追加のみ）。

### Requirement 3 — 明文化とテスト

**User Story:** As a **保守者**, I want **Warning 条件がコード上のコメントまたは定数で説明される**こと, so that **将来の変更で基準がぶれない**。

#### Acceptance Criteria — Requirement 3

1. WHEN **実装がマージ対象である** THEN **Warning を発火する条件がソースまたは `docs/` で参照可能**。
2. WHEN **テストが存在する** THEN **Warning 経路が最低 1 ケース検証される**。

## Non-Functional Requirements

### Code Architecture

- Warning の発火箇所は **Evaluator / FSM / WorkflowEngine** のいずれかに集約し、散在するマジックログを避ける。

### Performance

- Warning 判定は通常パスでオーバーヘッド最小（必要ならフラグでサンプリングは Out of Scope）。

## Out of Scope

- API 層の契約 Warning（別表。必要なら将来チケット）。
- ユーザー定義状態からの Warning（`STV-406` と連携し後続で拡張可）。

## References

- `.workspace-docs/50_tasks/10_in-progress/v2-logging-v1-tasks.md` — Warning 行
- `.spec-workflow/specs/engine-execution-logging/requirements.md` — STV-404
