# Logging v1 タスク一覧（Console / Structured）

- Version: 1.0.1
- 更新日: 2026-04-05
- 対象: API / Engine の v1.0.0 最低限の運用ログ（出力項目と実装タスク）
- 関連: `.workspace-docs/50_tasks/10_in-progress/v2-ticket-backlog.md`（STV-403〜STV-409）

---

## 現在の仕分け（2026-04-05）

- **完了**: `LOG-1`, `LOG-2`（受け入れ: `../20_done/v2-logging-stv403_backlog.md`, `../20_done/v2-logging-stv404_backlog.md`）
- **未完了**: `LOG-3` ～ `LOG-7`
- **見送り（今はしない）**: なし
- **棄却**: なし

---

## 1. 目的

API / Engine の実行状態を、開発・運用で追跡できる最小ログを定義する。  
本書は「**出力する項目**」と「**実装タスク**」を対応付ける。

---

## 2. ログ項目（最低限）

### API ログ

| 区分 | イベント | 必須項目 |
|------|----------|----------|
| Info | API リクエスト開始 | `traceId`, `method`, `path`, `tenantId`, `userAgent` |
| Info | API リクエスト完了 | `traceId`, `statusCode`, `elapsedMs`, `responseSize` |
| Warning | 契約上の注意（継続可能） | `traceId`, `code`, `message`, `details` |
| Error | 未処理例外 / 5xx | `traceId`, `errorType`, `message`, `stack`（環境で制御） |

### Engine ログ

| 区分 | イベント | 必須項目 |
|------|----------|----------|
| Info | workflow 開始 | `workflowId`, `definitionName`, `initialState` |
| Info | state 開始 | `workflowId`, `stateName`, `nodeId` |
| Info | state 完了 | `workflowId`, `stateName`, `fact`, `elapsedMs` |
| Warning | input 評価の注意 | `workflowId`, `stateName`, `inputKey`, `reason` |
| Warning | 遷移なしで停止 | `workflowId`, `stateName`, `fact` |
| Error | state 実行例外 | `workflowId`, `stateName`, `errorType`, `message` |
| Error | workflow 失敗 | `workflowId`, `reason` |

### ユーザー定義状態（IState）向け

| 区分 | イベント | 必須項目 |
|------|----------|----------|
| Info | ユーザー処理ログ | `workflowId`, `stateName`, `category`, `message` |
| Warning | 業務上の警告 | `workflowId`, `stateName`, `code`, `message` |
| Error | 業務処理エラー | `workflowId`, `stateName`, `errorType`, `message` |

---

## 3. タスク（実装順）

対象: Logging v1 実装タスク（LOG-1〜LOG-7）

| ID | 状態 | タスク | 優先度 | 依存 | 完了条件 | 備考 |
|----|------|--------|--------|------|----------|------|
| **LOG-1** | 完了 | API の基本リクエストログ導入 | P2 | - | 開始/完了/例外が `ILogger` で出る | `STV-403`。`../20_done/v2-logging-stv403_backlog.md` |
| **LOG-2** | 完了 | Engine の workflow/state ログ導入 | P2 | - | 開始/完了/失敗/キャンセルが出る | `STV-404`。`../20_done/v2-logging-stv404_backlog.md` |
| **LOG-3** | 未完了 | Warning ポリシー導入 | P2 | LOG-2 | 入力評価注意・遷移なし停止を Warning 出力 | `STV-405` |
| **LOG-4** | 未完了 | `StateContext` に Logger を追加 | P2 | LOG-2 | `IState` 実装が `ctx.Logger` でログ出力可能 | `STV-406` |
| **LOG-5** | 未完了 | ログフォーマット統一 | P2 | LOG-1, LOG-2 | `workflowId` / `stateName` / `traceId` のキー名統一 | `STV-407` |
| **LOG-6** | 未完了 | 秘密情報マスキング | P2 | LOG-5 | input/output のログ方針を実装・文書化 | `STV-408` |
| **LOG-7** | 未完了 | テスト（最低限） | P2 | LOG-1〜LOG-6 | ログ呼び出しの単体テスト（重要経路）追加 | `STV-409` |

---

## 4. 非目標（v1.0.0）

- OpenTelemetry の全面導入
- ログ集約基盤（ELK / Datadog）への本格連携
- ユーザー定義ログレベルの動的制御

---

## 5. 変更履歴

| 版 | 日付 | 内容 |
|----|------|------|
| 1.0.1 | 2026-04-05 | `LOG-1`/`LOG-2` を完了に更新。仕分けと完了記録パスを追記。 |
| 1.0.0 | 2026-04-02 | メタブロック・タスク表7列化。LOG-7 の依存を LOG-1〜LOG-6 に整合。文書 Version を 1.0.0 に揃える。 |
| 0.2 | 2026-04 | ステータス仕分けを追加。状態表記を「未着手」から「未完了」に統一。 |
| 0.1 | 2026-03 | 初版（ログ項目とタスクを定義） |
