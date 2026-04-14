# Tasks: ノード完了ごとの実行グラフ投影（API 内キュー）

**spec 名**: `workflow-projection-node-completion`  
**要件**: `requirements.md`  
**設計**: `design.md`

---

- [ ] **T1** — 承認反映と仕様入口の整備
  - `requirements.md` の承認状態、`approval-request.md`、`approval-status.json` の整合を維持する
  - `o6-subtickets-detailed` 側の参照が切れていないことを確認する
  - _要件: Requirement 1-6（前提）_

- [ ] **T2** — Engine 観測インターフェース追加
  - `IWorkflowEngine` にノード完了通知登録の公開 API を追加する
  - `WorkflowEngine` で通常ステート・Join 完了時に通知を発火する
  - Engine は DB 依存を持たない
  - _要件: Requirement 1, Requirement 2_

- [ ] **T3** — API 内キューの基盤実装
  - `IWorkflowProjectionUpdateQueue`（仮）と実装クラスを追加する
  - workflow 単位 1 スロット（coalesce）と有界グローバルキューを実装する
  - 満杯時ブロック（バックプレッシャー）・ドロップ禁止を満たす
  - _要件: Requirement 3_

- [ ] **T4** — デバウンスと単調性の実装
  - `ProjectionFlushDebounceMs`（既定 50ms、0-250ms）設定を追加する
  - デバウンス 0ms と >0ms の両モードを実装する
  - 併合後も「巻き戻りなし」の単調性を崩さないことを確認する
  - _要件: Requirement 3_

- [ ] **T5** — WorkflowService 連携
  - 既存 `BuildProjectionFromEngine` / `UpdateWorkflowAndSnapshotAsync` をキューワーカーから利用可能にする
  - 必要なら `UpdateProjectionAsync` の可視性/責務を調整する
  - _要件: Requirement 1, Requirement 3_

- [ ] **T6** — Cancel / Events 経路の整合
  - `POST .../cancel` と `POST .../events` の前に workflow 単位ドレイン（または等価ロック順序）を導入する
  - `event_store` 同一トランザクション経路との競合を防ぐ
  - _要件: Requirement 4_

- [ ] **T7** — HostedService と graceful shutdown
  - キューワーカーを `IHostedService` として登録し、停止時ドレインを実装する
  - タイムアウト時の未処理件数を構造化ログへ出力する
  - _要件: Requirement 5_

- [ ] **T8** — 設定追加と DI
  - `WorkflowProjectionQueueOptions` を追加する
  - `Program.cs` に options / queue / worker の DI 登録を追加する
  - _要件: Requirement 3, Requirement 5_

- [x] **T9** — リトライ制御と dead-letter 退避
  - 投影更新失敗時の **retry 上限**（例: `MaxRetryAttempts`）を導入する
  - retry 間隔に **バックオフ**（固定/指数のいずれかを設計で確定）を導入する
  - 上限到達時は対象 `workflow_id` を **dead-letter**（永続テーブルまたは運用キュー）へ退避し、再試行ループから外す
  - dead-letter 退避時は構造化ログとメトリクスを出力し、手動リカバリ手順をドキュメント化する
  - _要件: Requirement 3, Requirement 5, Non-Functional（可観測性）_

- [-] **T10** — テスト（Engine / API）
  - Engine 通知発火（通常・Join）を単体テストで追加する
  - Queue の coalesce・満杯ブロック・デバウンス 0/50ms を単体テストで追加する
  - retry 上限・バックオフ・dead-letter 遷移の単体/統合テストを追加する
  - Cancel/Events との競合防止、shutdown ドレインを統合テストで確認する
  - _要件: Requirement 1-5, Non-Functional（テスト）_

- [ ] **T11** — SSE 非変更の回帰確認
  - `GET /v1/workflows/{id}/stream` の約 2 秒ポーリング挙動が変わらないことを確認する
  - 必要なら既存テスト（`WorkflowStreamServiceTests`）を補強する
  - _要件: Requirement 6_

- [ ] **T12** — 最終検証とドキュメント同期
  - `dotnet test`（該当範囲）と必要な型チェックを実施する
  - `docs/statevia-data-integration-contract.md` と実装差分がないか確認し、差分があれば doc を更新する
  - _要件: Non-Functional（ドキュメント）_

---

## 実行メモ

- 着手中は `[ ]` を `[-]`、完了後は `[x]` に更新する。
- 各タスク完了時は必要に応じて Implementation Logs を追記する。
- `event_store` への `NODE_*` 追加、SSE 間隔変更は本タスク群の対象外。
