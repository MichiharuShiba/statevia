# Requirements: ログキー名の統一（STV-407 / LOG-5）

## Introduction

Core-API（HTTP リクエストログ）と Engine（実行ログ）および将来の `StateContext` ログで、**構造化ログのプロパティ名**を揃え、ログ基盤での検索・ダッシュボード定義を容易にする。

**紐づくチケット**: `STV-407`（`v2-ticket-backlog.md`）、`LOG-5`（`v2-logging-v1-tasks.md`）。

**依存**: `STV-403`（API）、`STV-404`（Engine）。

## Alignment with Product Vision

一貫した観測可能性は、マルチレイヤ障害の相関に不可欠。

## Requirements

### Requirement 1 — 命名規約の文書化

**User Story:** As a **運用者**, I want **公式のログキー一覧がある**こと, so that **クエリを共有できる**。

#### Acceptance Criteria — Requirement 1

1. WHEN **本仕様が完了する** THEN **`docs/` または `AGENTS.md` に、少なくとも次のキーの表がある**: `traceId`, `workflowId`, `stateName`, `tenantId`, `definitionId`（または display 相当の別名を明記）。
2. WHEN **表が存在する** THEN **API と Engine で同一概念に同一キー名**（大小文字規則を 1 つに固定）。

### Requirement 2 — コードへの適用

**User Story:** As a **保守者**, I want **実装が表に従う**こと, so that **ドリフトしない**。

#### Acceptance Criteria — Requirement 2

1. WHEN **Core-API のリクエストログが出力される** THEN **キー名は規約に従う**（既存実装のリネームを含む）。
2. WHEN **Engine 実行ログが出力される** THEN **同様に従う**。

### Requirement 3 — 互換性

**User Story:** As a **運用者**, I want **リネームでログが一瞬だけ二重定義にならない**こと, so that **移行が追いやすい**。

#### Acceptance Criteria — Requirement 3

1. IF **キー名を変更する** THEN **design で移行メモ（または 1 リリースだけ旧キー併記）を定義する**（任意）。

## Non-Functional Requirements

### Maintainability

- 共有定数クラス（例: `LogPropertyNames`）を **API と Engine のどちらに置くか**は design で決定（重複回避のため **共通ライブラリ**は Out of Scope なら、**文字列定数のコピー**は tasks で許容）。

## Out of Scope

- OpenTelemetry 属性名との完全一致（将来）。
- `STV-408` のマスキング内容。

## References

- `.spec-workflow/specs/api-request-basic-logging/design.md`
- `.spec-workflow/specs/engine-execution-logging/design.md`
- `v2-logging-v1-tasks.md` — LOG-5
