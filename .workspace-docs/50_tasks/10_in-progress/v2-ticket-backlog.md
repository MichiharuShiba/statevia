# v2 残タスク チケット一覧

- Version: 1.3.2
- 更新日: 2026-04-10
- 対象: `v2-remaining-tasks.md` / `v2-logging-v1-tasks.md` の未完了項目に紐づく実行チケット
- 関連: `.workspace-docs/50_tasks/10_in-progress/v2-remaining-tasks.md`, `.workspace-docs/50_tasks/10_in-progress/v2-logging-v1-tasks.md`, `.workspace-docs/30_specs/10_in-progress/o6-concerns_decomposition_spec.md`, `.workspace-docs/30_specs/10_in-progress/o6-subtickets_detailed_spec.md`

---

> 配置ポリシー: 本ファイルは実行チケットの正本として `.workspace-docs/50_tasks/10_in-progress/` で管理する。完了したチケット群は `.workspace-docs/50_tasks/20_done/` へ移動し、仕様差分は `.workspace-docs/30_specs/` に反映する。

---

## 現在の仕分け（2026-04-10）

- **完了**: `STV-401`～`STV-410`（`STV-401`/`402`: `../20_done/v2-e2e-cancel-idempotency_backlog.md`。`STV-403`: `../20_done/v2-logging-stv403_backlog.md`。`STV-404`: `../20_done/v2-logging-stv404_backlog.md`。`STV-410`: 変更履歴 1.3.0）
- **未完了**: `STV-413`～`STV-418`
- **見送り（今はしない）**: `STV-411`（認証エピック連動）
- **棄却**: なし

---

## 優先度ルール

- **P1**: 直近スプリントで着手（品質・回帰防止に直結）
- **P2**: 早めに実施（機能拡張の前提）
- **P3**: 認証など将来エピックと同時実施

---

## チケット一覧（実装順）

対象: STV-401〜STV-418

| ID | 状態 | タスク | 優先度 | 依存 | 完了条件 | 備考 |
|----|------|--------|--------|------|----------|------|
| STV-401 | 完了 | E2E（Cancel シーケンス）を追加 | P1 | - | Core-API 実体で Cancel が green、`cancelled`（または契約上の終端）を検証。CI は環境変数ゲート可。 | 実装・受け入れ: `../20_done/v2-e2e-cancel-idempotency_backlog.md` |
| STV-402 | 完了 | E2E（冪等・409）を追加 | P1 | STV-401 | 冪等挙動が契約どおり。409 の UI 表示を E2E で検証。再実行安定。 | 同上 |
| STV-403 | 完了 | API リクエスト基本ログを導入 | P2 | - | 主要エンドポイントで開始/完了ログが出る。`traceId` で相関可能。検証手順またはテストを文書化。 | 元: LOG-1。実装・受け入れ: `../20_done/v2-logging-stv403_backlog.md` |
| STV-404 | 完了 | Engine 実行ログを導入 | P2 | - | workflowId/stateName を含むログ。失敗経路で Error。 | 元: LOG-2。spec: `engine-execution-logging`。実装・受け入れ: `../20_done/v2-logging-stv404_backlog.md` |
| STV-405 | 完了 | Warning ポリシーを実装 | P2 | STV-404 | Warning 条件がコードで明文化。テストで最低1ケース。 | 元: LOG-3。spec: `logging-warning-policy` |
| STV-406 | 完了 | `StateContext` に Logger を追加 | P2 | STV-404 | サンプル state で `ctx.Logger` 利用可。後方互換維持。 | 元: LOG-4。spec: `state-context-logger` |
| STV-407 | 完了 | ログキー名を統一 | P2 | STV-403, STV-404 | 命名規約を docs に記載。API/Engine で適用。 | 元: LOG-5。spec: `unified-logging-key-names` |
| STV-408 | 完了 | input/output のマスキング方針を実装 | P2 | STV-407 | 代表機密キーがマスク。テストで生データ非露出。 | 元: LOG-6。spec: `workflow-io-log-masking`。ユーザー定義ルールは後続バックログで管理 |
| STV-412 | 見送り | ユーザー定義マスキングと外部テンプレート化 | P3 | STV-408 | 外部ファイル（テンプレート）によるルール読込と、ビルトイン既定とのマージが定義・検証される。 | 元: STV-408 Out of Scope（リリース後検討） |
| STV-409 | 完了 | ログ関連テストを追加 | P2 | STV-403〜STV-408 | テスト green。回帰検知可能。 | 元: LOG-7。spec: `logging-regression-tests` |
| STV-410 | 完了 | 懸念事項 O6 を仕様化して分割 | P2 | - | サブチケット5件以上粒度。優先度・依存整理。 | 元: O6。spec: `concern-o6-decomposition`、成果物: `.workspace-docs/30_specs/10_in-progress/o6-concerns_decomposition_spec.md` |
| STV-413 | 未完了 | C2: projection 更新タイミングの統一仕様化 | P1 | - | コマンド戻り値/コールバックの更新順序とトランザクション境界を 1 仕様に統合する。 | O6。仕様: `o6-subtickets_detailed_spec.md`（STV-413 節） |
| STV-414 | 未完了 | C7: Engine イベントと event_store 対応表の策定 | P1 | STV-413 | イベント種別・発火契機・payload・保存先の対応表を定義する。 | O6。仕様: `o6-subtickets_detailed_spec.md`（STV-414 節） |
| STV-415 | 未完了 | C11: コールバック失敗時の再送べき等仕様化 | P1 | STV-413, STV-414 | `event_id` 重複排除・リトライ戦略・失敗時観測性を仕様化する。 | O6。仕様: `o6-subtickets_detailed_spec.md`（STV-415 節） |
| STV-416 | 未完了 | C13: GetSnapshot と reducer 出力の整合方針決定 | P2 | STV-414 | スナップショットの正と差分検証方針を API/Engine で合意する。 | O6。仕様: `o6-subtickets_detailed_spec.md`（STV-416 節） |
| STV-417 | 未完了 | C14: nodes 未対応要素の段階導入計画策定 | P2 | STV-413 | nodes 固有フィールドの優先順位と未対応時の契約を定義する。 | O6。仕様: `o6-subtickets_detailed_spec.md`（STV-417 節） |
| STV-418 | 未完了 | O6 横断: 懸念対応ロードマップ統合 | P2 | STV-413, STV-414, STV-415, STV-416, STV-417 | O6 サブチケット群の依存順とスプリント配置を統合し追跡可能にする。 | O6。仕様: `o6-subtickets_detailed_spec.md`（STV-418 節） |
| STV-411 | 見送り | テナント管理機能 O7 の設計チケット化 | P3 | 認証機能エピックの方針確定 | 認証エピック向け前提設計の文書化。実装チケットに分割可能なアウトライン。 | 元: O7 |

> 詳細スコープ・受け入れ条件は以下の各チケット節を参照。  
> **`STV-401` / `STV-402` の節は完了により `../20_done/v2-e2e-cancel-idempotency_backlog.md` へ移した。**  
> **`STV-403` の節は完了により `../20_done/v2-logging-stv403_backlog.md` へ移した。**  
> **`STV-404` の節は完了により `../20_done/v2-logging-stv404_backlog.md` へ移した。**

### STV-405: Warning ポリシーを実装

- **spec-workflow**: `.spec-workflow/specs/logging-warning-policy/`
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

- **spec-workflow**: `.spec-workflow/specs/state-context-logger/`
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

- **spec-workflow**: `.spec-workflow/specs/unified-logging-key-names/`
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

- **spec-workflow**: `.spec-workflow/specs/workflow-io-log-masking/`
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

- **spec-workflow**: `.spec-workflow/specs/logging-regression-tests/`
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

### STV-412: ユーザー定義マスキングと外部テンプレート化

- 優先度: **P3**
- 目的: プロダクト標準ルールに加えて、運用側で独自キーをマスク対象へ追加できるようにする。あわせて、**マスキングルールをバイナリに焼かず外部テンプレート（設定ファイル）で差し替え可能**にする。
- スコープ:
  - **外部テンプレート**: ルール定義をリポジトリ外のファイル（YAML または JSON 等、形式は仕様で確定）として配置し、起動時（または運用で定めるタイミング）に読み込む。ファイルパスは環境変数またはアプリ設定で指定する。
  - 設定方式の設計（未指定時はビルトインのみ、パス不正・構文エラー時の挙動）
  - デフォルトルールとのマージ/優先順位の定義
  - 代表ユースケースのテスト（外部ファイルあり/なし、マージ結果、エラー時のフェイルセーフ）
- 受け入れ条件:
  - デフォルトルールを壊さずに追加ルールが適用される
  - 外部テンプレートを差し替えた場合に、文書化した手順で反映できる（再起動要否は仕様で明示）
  - 運用ドキュメントに設定手順とテンプレート例がある
- 依存: `STV-408`

### STV-410: 懸念事項 O6 を仕様化して分割

- **spec-workflow**: `.spec-workflow/specs/concern-o6-decomposition/`
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
- 成果物: `.workspace-docs/30_specs/10_in-progress/o6-concerns_decomposition_spec.md`、`.workspace-docs/30_specs/10_in-progress/o6-subtickets_detailed_spec.md`（サブチケット仕様）

### STV-413〜STV-418: O6 サブチケット

- 親チケット: `STV-410`
- **仕様正本**: `.workspace-docs/30_specs/10_in-progress/o6-subtickets_detailed_spec.md`（各 STV ごとの要件・現状実装・将来 U1 整合・完了条件）
- 分解概要: `.workspace-docs/30_specs/10_in-progress/o6-concerns_decomposition_spec.md`
- 運用:
  - `STV-413` を起点に `STV-414` / `STV-415` を先行
  - `STV-416` / `STV-417` は中盤で並行
  - `STV-418` で横断整理して O6 系の追跡を閉じる

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
- **Sprint B**: ~~`STV-403`~~ / ~~`STV-404`~~ / ~~`STV-405`~~ / ~~`STV-406`~~ / ~~`STV-407`~~ / ~~`STV-408`~~ / ~~`STV-409`~~ 完了（〜2026-04-09）
- **Sprint C**: ~~`STV-410`~~ 完了（2026-04-09）。着手対象: `STV-413`～`STV-418`
- **認証エピック連動**: `STV-411`
- **ポストリリース拡張**: `STV-412`（ユーザー定義マスキング・外部テンプレート）

---

## メモ

- チケット ID は仮番号（`STV-4xx`）。実運用の Issue 番号に置換して利用する。
- `O6` は実装前に必ず仕様化フェーズを挟む（いきなり実装しない）。

---

## 変更履歴

| 版 | 日付 | 内容 |
|----|------|------|
| 1.3.2 | 2026-04-10 | O6 サブチケットの仕様正本（`o6-subtickets_detailed_spec.md`）を追加。`STV-413`〜`STV-418` の備考を更新。 |
| 1.3.1 | 2026-04-10 | `STV-412` にマスキングの外部テンプレート化（外部ファイル読込・マージ・運用手順）を追記。 |
| 1.3.0 | 2026-04-09 | `STV-410` を完了に更新。O6 分解成果物（`o6-concerns_decomposition_spec.md`）を追加し、サブチケット `STV-413`〜`STV-418` を起票。 |
| 1.2.0 | 2026-04-09 | 実装状況に合わせて `STV-407`/`STV-408`/`STV-409` を完了に更新。仕分け（未完了は `STV-410` のみ）と Sprint を最新化。 |
| 1.1.0 | 2026-04-08 | `STV-412`（ユーザー定義マスキングルール）を見送りバックログとして追加。`STV-408` 備考に連携方針を追記。 |
| 1.0.9 | 2026-04-08 | `STV-405`/`STV-406` を完了に更新。仕分け・一覧・Sprint B の残件を最新化。 |
| 1.0.8 | 2026-04-05 | `STV-404` を完了に更新。詳細を `../20_done/v2-logging-stv404_backlog.md` へ移設。 |
| 1.0.7 | 2026-04-05 | `STV-404`〜`STV-410` の spec-workflow 起票（各 spec に `requirements.md` / `design.md` / `tasks.md`）。備考に spec フォルダ名を追記。 |
| 1.0.6 | 2026-04-05 | `STV-404` は未完了に戻す。完了記録は `STV-403` のみ `20_done/v2-logging-stv403_backlog.md` に整理。 |
| 1.0.5 | 2026-04-05 | `STV-403`/`STV-404` を完了に更新。詳細を `20_done/v2-logging-stv403-404_backlog.md` へ移設（1.0.6 で `STV-404` を差し戻し）。 |
| 1.0.4 | 2026-04-03 | `STV-403` spec に `tasks.md` 下書きを追加。 |
| 1.0.3 | 2026-04-03 | `STV-403` spec に `design.md` たたき台を追加。 |
| 1.0.2 | 2026-04-03 | `STV-403` を spec-workflow 起票（`api-request-basic-logging/requirements.md`）。 |
| 1.0.1 | 2026-04-03 | `STV-401`/`STV-402` を完了に更新。詳細を `20_done/v2-e2e-cancel-idempotency_backlog.md` へ移設。 |
| 1.0.0 | 2026-04-02 | メタブロック・チケット表7列化（完了条件・備考）。 |
