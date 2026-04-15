# Requirements: Engine 実行ログ（STV-404 / LOG-2）

## Introduction

`Statevia.Core.Engine` の **ワークフロー／状態実行**について、`ILogger` による構造化ログを追加する。`workflowId`・`stateName` 等で検索・相関し、失敗・キャンセル・完了を運用で追跡できるようにする。

**紐づくチケット**: `STV-404`（`v2-ticket-backlog.md`）、`LOG-2`（`v2-logging-v1-tasks.md`）。

## Alignment with Product Vision

定義駆動ワークフローエンジンは Core-API と in-process で連携する。Engine 側の実行ログは、インシデント調査と「どの状態で止まったか」の把握に直結し、イベントソース運用の可観測性を補完する。

## Requirements

### Requirement 1 — workflow ライフサイクル

**User Story:** As a **運用・開発者**, I want **workflow 開始および終端（完了・失敗・キャンセル）がログに残る**こと, so that **インスタンス単位で結果を追える**。

#### Acceptance Criteria — Requirement 1

1. WHEN **`IWorkflowEngine.Start` が新規インスタンスを起動する** THEN **システムは少なくとも `workflowId` と初期状態に関する文脈を Info ログに含める**。
2. WHEN **ワークフローが完了・失敗・協調的キャンセルで終了する** THEN **システムはそれぞれ区別可能なログ（レベルは design で定義）を出す**。
3. WHEN **上記ログが出力される** THEN **`v2-logging-v1-tasks.md` の Engine ログ表（workflow 開始）と矛盾しない必須項目を満たす**（`definitionName` / `initialState` 等。取得不能時は design でプレースホルダ可）。

### Requirement 2 — state 実行

**User Story:** As a **運用・開発者**, I want **各状態の開始と完了（fact 含む）がログに残る**こと, so that **どの state で何が起きたか追える**。

#### Acceptance Criteria — Requirement 2

1. WHEN **状態実行がスケジュールされる** THEN **システムは `workflowId`, `stateName`, （取得可能なら）`nodeId` を含む Info ログを出す**。
2. WHEN **状態実行が fact 付きで完了する** THEN **システムは `fact`, `elapsedMs`（計測する場合）を含む Info ログを出す**。
3. IF **Join 状態** THEN **ログの粒度は通常 state と整合し、同一表の Warning/Error と衝突しない**（design でイベント名を固定）。

#### `elapsedMs` の計測区間（Requirement 2 の補足）

1. **`elapsedMs` が指す期間**は、**当該状態について `IStateExecutor.ExecuteAsync` を起動する直前**から、**その呼び出しが完了し fact（`Completed` / `Failed` / `Cancelled` 等）がエンジン内で確定するまで**の **壁時計経過**（UTC 基準）をミリ秒で表す。
2. **Wait / Resume**: `ExecuteAsync` が外部イベント待ちで `await` している時間は **上記区間に含める**（待機込みの経過時間。突合せ用に「状態が占有していた時間」と解釈する）。
3. **Join など `ExecuteAsync` を伴わない合成経路**では `elapsedMs` を **省略する**か **0 または意味付きのプレースホルダ**とする（どちらかを design で固定）。
4. **「CPU だけ」「待ち除き」**の別メトリクスは本要件の範囲外（必要なら将来チケット）。

### Requirement 2b — ログ出力の手段（ライブラリ汚染の防止）

**User Story:** As a **Engine の単体利用者**, I want **ログ用のバックグラウンドスレッドやグローバル状態を Engine が持ち込まない**こと, so that **ホストに依存しない**。

#### Acceptance Criteria — Requirement 2b

1. WHEN **Engine がログを出す** THEN **手段は `Microsoft.Extensions.Logging` の標準 **同期** API（`ILogger` の `Log*` / `LoggerMessage` 等）に限定する**。Engine 内で **独自の非同期キュー・タイマー・イベントバスをログ専用に起動しない**。
2. WHEN **実際の I/O やバッチ送信が行われる** THEN **それはホストが登録した `ILoggerProvider` の責務**とし、Engine は **抽象 (`ILogger` / `Microsoft.Extensions.Logging.Abstractions`) のみ**に依存する。
3. WHEN **ロガーが未注入** THEN **既定は `NullLogger` 相当**とし、`new WorkflowEngine()` のような **単体利用でログ基盤の初期化を要求しない**。
4. WHEN **ログ プロバイダが例外を投げる** THEN **その例外は state 遷移に伝播させない**（ログ呼び出しを囲む try/catch の方針は design）。

**注:** 「同期 API」とは呼び出しスレッドで `Log*` が返ること（プロバイダ内部で非同期シンクしてよい）。

### Requirement 3 — 失敗経路

**User Story:** As a **運用・開発者**, I want **状態実行例外および workflow 失敗が Error で記録される**こと, so that **アラートと手掛かりを得られる**。

#### Acceptance Criteria — Requirement 3

1. WHEN **`IStateExecutor.ExecuteAsync` が例外で終了し、エンジンが失敗 fact を記録する** THEN **システムは `workflowId`, `stateName`, `errorType`, `message` を含む Error ログを出す**（スタックは本番抑制可。API リクエストログと同趣旨）。
2. WHEN **ワークフロー全体が失敗としてマークされる** THEN **システムは `workflowId` と理由を含む Error ログを出す**。

### Requirement 4 — 依存と非機能

**User Story:** As a **ライブラリ利用者**, I want **ログが実行セマンティクスを壊さず、テストで検証できる**こと, so that **本番と CI の両方で安全に使える**。

#### Acceptance Criteria — Requirement 4

1. WHEN **ロガーが未設定またはログ出力が失敗する** THEN **ワークフロー実行は従来どおり致命傷にしない**（Requirement 2b および design の try/catch）。
2. WHEN **実装がマージ対象である** THEN **単体テストで主要ログ経路が最低 1 ケース検証される**。

## Non-Functional Requirements

### Code Architecture and Modularity

- **単一責任**: ログは `WorkflowEngine`（または専用の実行ロガー補助型）に集約し、各 `IStateExecutor` 実装に重複させない（`STV-406` でユーザー定義ログを追加）。
- **依存**: `Microsoft.Extensions.Logging.Abstractions` のみを Engine に追加し、具象ログ基盤に依存しない。

### Performance

- ログはホットパスを著しく遅延させない（高頻度 `LoggerMessage` 等を検討）。

### Security

- **状態入出力や機密**は本文に出さない、または `STV-408` までマスキング方針に従う（本 spec では **識別子中心**に留める）。

### Reliability

- ログ失敗で state 遷移が壊れないこと。
- **ライブラリ境界**: グローバル静的ロガーへの依存を追加しない（インスタンス／オプション経由の注入のみ）。

## Out of Scope

- `StateContext` への `Logger` 公開（`STV-406`）。
- Warning ポリシーの本実装（`STV-405`）。
- ログキー名の全スタック統一（`STV-407`）。本 spec では既存 API ログと衝突しない命名で導入。

## References

- `.workspace-docs/50_tasks/10_in-progress/v2-ticket-backlog.md` — STV-404
- `.workspace-docs/50_tasks/10_in-progress/v2-logging-v1-tasks.md` — LOG-2、Engine ログ項目表
- `.spec-workflow/specs/api-request-basic-logging/requirements.md` — STV-403（相関の参考）
- `AGENTS.md` — Engine / Core-API 境界
