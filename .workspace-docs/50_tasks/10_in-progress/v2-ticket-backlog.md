# v2 残タスク チケット一覧

- Version: 1.0.4
- 更新日: 2026-04-03
- 対象: `v2-remaining-tasks.md` / `v2-logging-v1-tasks.md` の未完了項目に紐づく実行チケット
- 関連: `.workspace-docs/50_tasks/10_in-progress/v2-remaining-tasks.md`, `.workspace-docs/50_tasks/10_in-progress/v2-logging-v1-tasks.md`

---

> 配置ポリシー: 本ファイルは実行チケットの正本として `.workspace-docs/50_tasks/10_in-progress/` で管理する。完了したチケット群は `.workspace-docs/50_tasks/20_done/` へ移動し、仕様差分は `.workspace-docs/30_specs/` に反映する。

---

## 現在の仕分け（2026-04-03）

- **完了**: `STV-401`、`STV-402`（詳細: `../20_done/v2-e2e-cancel-idempotency_backlog.md`）
- **未完了**: `STV-403` ～ `STV-410`
- **見送り（今はしない）**: `STV-411`（認証エピック連動）
- **棄却**: なし

---

## 優先度ルール

- **P1**: 直近スプリントで着手（品質・回帰防止に直結）
- **P2**: 早めに実施（機能拡張の前提）
- **P3**: 認証など将来エピックと同時実施

---

## チケット一覧（実装順）

対象: STV-401〜STV-411

| ID | 状態 | タスク | 優先度 | 依存 | 完了条件 | 備考 |
|----|------|--------|--------|------|----------|------|
| STV-401 | 完了 | E2E（Cancel シーケンス）を追加 | P1 | - | Core-API 実体で Cancel が green、`cancelled`（または契約上の終端）を検証。CI は環境変数ゲート可。 | 実装・受け入れ: `../20_done/v2-e2e-cancel-idempotency_backlog.md` |
| STV-402 | 完了 | E2E（冪等・409）を追加 | P1 | STV-401 | 冪等挙動が契約どおり。409 の UI 表示を E2E で検証。再実行安定。 | 同上 |
| STV-403 | 未完了 | API リクエスト基本ログを導入 | P2 | - | 主要エンドポイントで開始/完了ログが出る。`traceId` で相関可能。検証手順またはテストを文書化。 | 元: LOG-1。spec: `api-request-basic-logging`（requirements / design / **tasks** 下書き済。実装は tasks 承認後） |
| STV-404 | 未完了 | Engine 実行ログを導入 | P2 | - | workflowId/stateName を含むログ。失敗経路で Error。 | 元: LOG-2 |
| STV-405 | 未完了 | Warning ポリシーを実装 | P2 | STV-404 | Warning 条件がコードで明文化。テストで最低1ケース。 | 元: LOG-3 |
| STV-406 | 未完了 | `StateContext` に Logger を追加 | P2 | STV-404 | サンプル state で `ctx.Logger` 利用可。後方互換維持。 | 元: LOG-4 |
| STV-407 | 未完了 | ログキー名を統一 | P2 | STV-403, STV-404 | 命名規約を docs に記載。API/Engine で適用。 | 元: LOG-5 |
| STV-408 | 未完了 | input/output のマスキング方針を実装 | P2 | STV-407 | 代表機密キーがマスク。テストで生データ非露出。 | 元: LOG-6 |
| STV-409 | 未完了 | ログ関連テストを追加 | P2 | STV-403〜STV-408 | テスト green。回帰検知可能。 | 元: LOG-7 |
| STV-410 | 未完了 | 懸念事項 O6 を仕様化して分割 | P2 | - | サブチケット5件以上粒度。優先度・依存整理。 | 元: O6 |
| STV-411 | 見送り | テナント管理機能 O7 の設計チケット化 | P3 | 認証機能エピックの方針確定 | 認証エピック向け前提設計の文書化。実装チケットに分割可能なアウトライン。 | 元: O7 |

> 詳細スコープ・受け入れ条件は以下の各チケット節を参照。  
> **`STV-401` / `STV-402` の節は完了により `../20_done/v2-e2e-cancel-idempotency_backlog.md` へ移した。**

### STV-403: API リクエスト基本ログを導入

- **spec-workflow**: `.spec-workflow/specs/api-request-basic-logging/`（`requirements.md` / `design.md` / `tasks.md` 下書きまで。承認後に実装フェーズ）
- 優先度: **P2**
- 元タスク: `logging-v1-tasks.md` の `LOG-1`
- 目的: API の開始/完了/例外を追跡可能にする
- スコープ:
  - request 開始ログ（`traceId`, `method`, `path`, `tenantId`）
  - request 完了ログ（`statusCode`, `elapsedMs`）
  - 例外ログ（5xx）
- 受け入れ条件:
  - 主要エンドポイントで開始/完了ログが出力される
  - `traceId` で相関できる
  - テストまたは検証手順をドキュメント化
- 依存: なし

### STV-404: Engine 実行ログを導入

- 優先度: **P2**
- 元タスク: `logging-v1-tasks.md` の `LOG-2`
- 目的: workflow/state のライフサイクルを可視化する
- スコープ:
  - workflow 開始/終了（完了・失敗・キャンセル）
  - state 開始/完了
- 受け入れ条件:
  - workflowId/stateName を含むログが出る
  - 失敗経路で Error ログが出る
- 依存: なし

### STV-405: Warning ポリシーを実装

- 優先度: **P2**
- 元タスク: `logging-v1-tasks.md` の `LOG-3`
- 目的: 継続可能な異常を Warning で一貫出力する
- スコープ:
  - input 評価注意
  - 遷移なし停止
- 受け入れ条件:
  - Warning 出力条件がコードで明文化される
  - テストで最低1ケース検証
- 依存: `STV-404`

### STV-406: `StateContext` に Logger を追加

- 優先度: **P2**
- 元タスク: `logging-v1-tasks.md` の `LOG-4`
- 目的: `IState` 実装から文脈付きログを出せるようにする
- スコープ:
  - `StateContext` に `Logger`（または同等インターフェース）追加
  - 既存 executor 呼び出しに注入
- 受け入れ条件:
  - サンプル state で `ctx.Logger` が利用可能
  - 既存実行パスの後方互換を維持
- 依存: `STV-404`

### STV-407: ログキー名を統一

- 優先度: **P2**
- 元タスク: `logging-v1-tasks.md` の `LOG-5`
- 目的: 検索しやすい構造化ログへ統一する
- スコープ:
  - `workflowId`, `stateName`, `traceId` などキーを標準化
  - API/Engine で命名揺れを排除
- 受け入れ条件:
  - ログキー命名規約を docs に記載
  - API/Engine 両方で規約適用
- 依存: `STV-403`, `STV-404`

### STV-408: input/output のマスキング方針を実装

- 優先度: **P2**
- 元タスク: `logging-v1-tasks.md` の `LOG-6`
- 目的: 秘密情報露出リスクを低減する
- スコープ:
  - マスキング対象キーのルール定義
  - ログ出力前のマスキング処理
  - 運用ドキュメント反映
- 受け入れ条件:
  - 代表的な機密キーがマスクされる
  - ログに生データが出ないことをテストで確認
- 依存: `STV-407`

### STV-409: ログ関連テストを追加

- 優先度: **P2**
- 元タスク: `logging-v1-tasks.md` の `LOG-7`
- 目的: 重要ログ経路の回帰を防止する
- スコープ:
  - API/Engine の主要ログ呼び出しを単体テスト化
  - 失敗・警告経路を最低1ケースずつ検証
- 受け入れ条件:
  - テストが安定して green
  - 将来変更で回帰検知可能
- 依存: `STV-403`〜`STV-408`

### STV-410: 懸念事項 O6 を仕様化して分割

- 優先度: **P2**
- 元タスク: `remaining-tasks.md` の `O6`
- 目的: 大きな未確定課題を実装可能な粒度へ分解する
- スコープ:
  - C2/C7/C11/C13/C14 を個別チケットに分割
  - 各項目の「仕様未確定点」と「実装方針」を記載
- 受け入れ条件:
  - サブチケット（5件以上）が起票可能な粒度
  - 優先度と依存が整理されている
- 依存: なし

### STV-411: テナント管理機能 O7 の設計チケット化

- 優先度: **P3**
- 元タスク: `remaining-tasks.md` の `O7`
- 目的: 認証導入時に着手できる設計前提を準備する
- スコープ:
  - テナント管理 API（登録/一覧/無効化）案
  - テナント検証ポリシー案
  - JWT/APIキーとの統合方針（草案）
- 受け入れ条件:
  - 認証エピックに紐づく前提設計が文書化される
  - 実装チケットに分割可能なアウトラインがある
- 依存: 認証機能エピックの方針確定

---

## 推奨マイルストーン

- **Sprint A**: ~~`STV-401`, `STV-402`~~ 完了（2026-04-03）
- **Sprint B**: `STV-403`〜`STV-407`
- **Sprint C**: `STV-408`, `STV-409`, `STV-410`
- **認証エピック連動**: `STV-411`

---

## メモ

- チケット ID は仮番号（`STV-4xx`）。実運用の Issue 番号に置換して利用する。
- `O6` は実装前に必ず仕様化フェーズを挟む（いきなり実装しない）。

---

## 変更履歴

| 版 | 日付 | 内容 |
|----|------|------|
| 1.0.4 | 2026-04-03 | `STV-403` spec に `tasks.md` 下書きを追加。 |
| 1.0.3 | 2026-04-03 | `STV-403` spec に `design.md` たたき台を追加。 |
| 1.0.2 | 2026-04-03 | `STV-403` を spec-workflow 起票（`api-request-basic-logging/requirements.md`）。 |
| 1.0.1 | 2026-04-03 | `STV-401`/`STV-402` を完了に更新。詳細を `20_done/v2-e2e-cancel-idempotency_backlog.md` へ移設。 |
| 1.0.0 | 2026-04-02 | メタブロック・チケット表7列化（完了条件・備考）。 |
