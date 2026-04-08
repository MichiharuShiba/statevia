# Requirements: workflow input / state output のログマスキング（STV-408 / LOG-6）

## Introduction

`workflowInput` および状態 `output` は契約上センシティブになり得る（`AGENTS.md` IO-14）。**ログにこれらを載せる経路**では、代表キーをマスクし、テストで生値が漏れないことを保証する。

**紐づくチケット**: `STV-408`（`v2-ticket-backlog.md`）、`LOG-6`（`v2-logging-v1-tasks.md`）。

**依存**: `STV-407`（キー名の前提が固まっていると Redactor のフィールド列挙が容易）。

## Alignment with Product Vision

データ連携契約に沿った取り扱いは、外部ログ基盤流出リスクを下げる。

## Requirements

### Requirement 1 — マスキングルール

**User Story:** As a **セキュリティ担当**, I want **パスワード・トークン等の代表キーがマスクされる**こと, so that **ログ流出の影響を限定できる**。

#### Acceptance Criteria — Requirement 1

1. WHEN **JSON またはクエリ文字列がログに載る** THEN **`password`, `token`, `secret`, `authorization` 等の代表キーがマスクされる**（`LogBodyRedactor` と整合。リストは design で拡張可能）。
2. WHEN **ワークフロー系ペイロードをログする** THEN **ネストしたキーも走査対象**（浅い辞書のみは不可）。

### Requirement 2 — 適用箇所

**User Story:** As a **保守者**, I want **HTTP ログと Engine ログの両方で同じ方針**が使われること, so that **レビューが一箇所で済む**。

#### Acceptance Criteria — Requirement 2

1. WHEN **Core-API がリクエスト/レスポンス本文をログする** THEN **既存 Redactor が要件を満たす**（差分があれば拡張）。
2. WHEN **Engine が state 出力をログに載せる設計になる** THEN **同一 Redactor または共有ヘルパを利用**（重複実装を避ける。共通化の物理配置は design）。

### Requirement 3 — テスト

**User Story:** As a **保守者**, I want **テストでマスク漏れが検知できる**こと, so that **回帰しない**。

#### Acceptance Criteria — Requirement 3

1. WHEN **実装がマージ対象である** THEN **代表機密キーを含むサンプル JSON でログ出力がマスクされるテストがある**。

## Non-Functional Requirements

### Performance

- 巨大ペイロードは **バイト上限で切り詰め**（STV-403 方針と整合）。

## Out of Scope

- 全フィールドの暗号化保管。
- ユーザー定義のマスキングルールファイル（YAML）。※後続バックログ `STV-412` として追加済み。

## References

- `docs/statevia-data-integration-contract.md` — IO-14
- `api/Statevia.Core.Api/Hosting/LogBodyRedactor.cs`
