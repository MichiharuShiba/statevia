# Requirements: StateContext に Logger を追加（STV-406 / LOG-4）

## Introduction

`StateContext`（状態実行コンテキスト）に **`ILogger` または薄いラッパ**を公開し、`IStateExecutor` / ユーザー定義状態から **文脈付きログ**（`workflowId`, `stateName` 自動付与）を出力できるようにする。

**紐づくチケット**: `STV-406`（`v2-ticket-backlog.md`）、`LOG-4`（`v2-logging-v1-tasks.md`）。

**依存**: `STV-404`（Engine 実行ログとロガー注入パターンの整合）。

## Alignment with Product Vision

拡張状態実装はエンジンの主要拡張点。デバッグ・業務ログをユーザーコードに委ねる際、テナントや API トレースと独立した **Engine スコープのロガー**が必要。

## Requirements

### Requirement 1 — API 公開

**User Story:** As a **状態実装者**, I want **`StateContext` からロガーを取得できる**こと, so that **追加の引数なしで文脈付きログを書ける**。

#### Acceptance Criteria — Requirement 1

1. WHEN **`StateContext` が `WorkflowEngine` から生成される** THEN **`Logger` プロパティ（または `GetLogger()`）が利用可能**（型は `ILogger` または `ILogger<StateContext>` 等。design で確定）。
2. WHEN **ロガーが未設定の場合** THEN **ノーログまたは `NullLogger` で既存コードがビルド可能**（後方互換）。

### Requirement 2 — 文脈の自動付与

**User Story:** As a **状態実装者**, I want **ログに `workflowId` と `stateName` が含まれる**こと, so that **検索しやすい**。

#### Acceptance Criteria — Requirement 2

1. WHEN **`ctx.Logger` でログを書く** THEN **出力には少なくとも `WorkflowId` と `StateName` が構造化プロパティとして付く**（`LoggerMessage` スコープ or 外側で `BeginScope`）。
2. IF **`STV-407` が未完了** THEN **本実装のキー名は `STV-404` の Engine ログと整合する命名で仮固定**。

### Requirement 3 — サンプルと後方互換

**User Story:** As a **リポジトリ保守者**, I want **サンプル state で利用例が示される**こと, so that **拡張の手本になる**。

#### Acceptance Criteria — Requirement 3

1. WHEN **リリース対象である** THEN **サンプル定義またはテスト用 executor で `ctx.Logger` の使用例が 1 か所ある**。
2. WHEN **既存の `StateContext` 生成箇所** THEN **すべてコンパイルが通る**（新プロパティは nullable または既定で `NullLogger`）。

## Non-Functional Requirements

### Security

- ユーザーがログに **workflowInput / 出力オブジェクト** をそのまま渡さないよう、`docs` または XML コメントで IO-14 を参照。

## Out of Scope

- ログキー名の全スタック統一（`STV-407`）。
- マスキング本格実装（`STV-408`）。

## References

- `engine/Statevia.Core.Engine/Abstractions/StateContext.cs`
- `v2-logging-v1-tasks.md` — ユーザー定義ログ表
