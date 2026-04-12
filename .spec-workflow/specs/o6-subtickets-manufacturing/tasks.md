# Tasks: O6 サブチケット製造フェーズ（STV-413〜STV-418）

- [x] 1. 冪等配送テーブルとモデルを追加する
  - 対象ファイル: `api/Statevia.Core.Api/Persistence/CoreDbContext.cs`, `api/Statevia.Core.Api/Persistence/Models/EventDeliveryDedupRow.cs`, `api/Statevia.Core.Api/Persistence/Migrations/*`
  - `event_delivery_dedup`（`tenant_id`, `workflow_id`, `client_event_id`, `batch_id`, `status`, `accepted_at` など）を追加し、`UNIQUE(tenant_id, workflow_id, client_event_id)` を定義する
  - 目的: STV-415 の正本冪等判定を DB で担保する
  - _活用: `CommandDedupRow`, 既存 EF Core migration パターン_
  - _要件: Requirement 3, Requirement 6_
  - _Prompt: spec `o6-subtickets-manufacturing` のタスクを実装してください。最初に `spec-workflow-guide` を実行し、ワークフローを確認してから着手してください。役割: EF Core スキーマ設計に強い C# バックエンド開発者 | 作業: 既存永続化規約に合わせて、`(tenant_id, workflow_id, client_event_id)` を一意キーとする配送 dedup テーブルとモデルを追加する | 制約: 既存テーブルを壊さないこと、既存 command dedup 挙動を削除しないこと、migration はロールバック可能に保つこと | 活用: `CommandDedupRow` モデルと既存 migration の命名・実装パターン | 要件: Requirement 3, Requirement 6 | 完了条件: migration が正常適用でき、テーブルと index が意図どおり作成され、既存テスト/ビルドが green。着手時は tasks.md の当該タスクを `[-]` に変更し、実装後は `log-implementation` を記録して `[x]` に更新する。_

- [x] 2. 専用 dedup リポジトリとステータス更新 API を実装する
  - 対象ファイル: `api/Statevia.Core.Api/Abstractions/Persistence/IEventDeliveryDedupRepository.cs`, `api/Statevia.Core.Api/Persistence/Repositories/EventDeliveryDedupRepository.cs`, `api/Statevia.Core.Api/Program.cs`
  - `RECEIVED` / `APPLIED` / `FAILED` を扱う repository API を追加し、DI 登録する
  - 目的: トランザクション分離で受理状態を先行保存できるようにする
  - _活用: `ICommandDedupRepository`, 既存 repository 実装_
  - _要件: Requirement 3_
  - _Prompt: spec `o6-subtickets-manufacturing` のタスクを実装してください。最初に `spec-workflow-guide` を実行し、ワークフローを確認してから着手してください。役割: C# リポジトリ層エンジニア | 作業: event delivery dedup の状態遷移（received/applied/failed）を扱う interface/実装を作成し、DI に接続する | 制約: repository は永続化責務に限定すること、repository メソッドに業務ロジックを入れないこと、既存依存関係を壊さないこと | 活用: `ICommandDedupRepository` と既存 repository の tx パターン | 要件: Requirement 3 | 完了条件: lookup/create/status-update を実行できる repository が DI から解決できる。着手時は tasks.md の当該タスクを `[-]` に変更し、実装後は `log-implementation` を記録して `[x]` に更新する。_

- [x] 3. Engine 公開インターフェースを clientEventId 対応で拡張する
  - 対象ファイル: `engine/Statevia.Core.Engine/Abstractions/IWorkflowEngine.cs`, `engine/Statevia.Core.Engine/Engine/WorkflowEngine.cs`, `engine/Statevia.Core.Engine.Tests/*`
  - `PublishEvent` / `CancelAsync` に `clientEventId` を受けるオーバーロードを追加し、後方互換シグネチャを維持する
  - 目的: STV-415 の Engine 側 `AlreadyApplied` 判定の土台を作る
  - _活用: 既存 `PublishEvent` / `CancelAsync` 実装とテスト_
  - _要件: Requirement 3_
  - _Prompt: spec `o6-subtickets-manufacturing` のタスクを実装してください。最初に `spec-workflow-guide` を実行し、ワークフローを確認してから着手してください。役割: C# ワークフローエンジンのランタイム開発者 | 作業: 後方互換を維持したまま、`PublishEvent` と `CancelAsync` に clientEventId 対応オーバーロードを追加する | 制約: 既存シグネチャを削除しないこと、clientEventId なし呼び出しの挙動を変えないこと | 活用: `WorkflowEngine` の既存コマンド実装と関連ユニットテスト | 要件: Requirement 3 | 完了条件: オーバーロードがコンパイルでき、既存呼び出しが維持され、キーあり/なし両経路のテストが通る。着手時は tasks.md の当該タスクを `[-]` に変更し、実装後は `log-implementation` を記録して `[x]` に更新する。_

- [x] 4. Engine 内で AlreadyApplied 判定と結果モデルを実装する
  - 対象ファイル: `engine/Statevia.Core.Engine/Engine/*`, `engine/Statevia.Core.Engine/Abstractions/*`, `engine/Statevia.Core.Engine.Tests/*`
  - `ApplyResult`（`Applied` / `AlreadyApplied`）を導入し、同一 `clientEventId` の再適用時は No-Op を返す
  - 目的: ロールバック後再送時の二重適用を Engine 側で抑止する
  - _活用: Workflow インスタンス管理ロジック、既存スナップショット取得_
  - _要件: Requirement 3, Requirement 4_
  - _Prompt: spec `o6-subtickets-manufacturing` のタスクを実装してください。最初に `spec-workflow-guide` を実行し、ワークフローを確認してから着手してください。役割: 冪等遷移に強い C# ドメインエンジニア | 作業: `ApplyResult` を追加し、同一 clientEventId の重複入力時に状態を変えず `AlreadyApplied` を返す判定を実装する | 制約: 現行のスレッドセーフ前提を壊さないこと、グローバル可変状態を増やさないこと、ID なし経路の挙動を維持すること | 活用: 現在の instance lifecycle と snapshot/export 処理 | 要件: Requirement 3, Requirement 4 | 完了条件: 重複イベントで `AlreadyApplied` を返し状態不変がテストで保証される。着手時は tasks.md の当該タスクを `[-]` に変更し、実装後は `log-implementation` を記録して `[x]` に更新する。_

- [x] 5. WorkflowService を dedup 正本フローに置き換える
  - 対象ファイル: `api/Statevia.Core.Api/Services/WorkflowService.cs`, `api/Statevia.Core.Api/Abstractions/Services/IWorkflowService.cs`, `api/Statevia.Core.Api/Controllers/*`
  - 先行 `RECEIVED` 保存 → Engine 実行 → DB 更新 → `APPLIED` 更新の流れを実装し、`AlreadyApplied` は HTTP 204 固定にする
  - 目的: 設計で合意した API/DB 正本の再送処理を適用する
  - _活用: 既存 tx 制御、`BuildProjectionFromEngine`, command dedup 処理_
  - _要件: Requirement 1, Requirement 3, Requirement 4_
  - _Prompt: spec `o6-subtickets-manufacturing` のタスクを実装してください。最初に `spec-workflow-guide` を実行し、ワークフローを確認してから着手してください。役割: Core-API サービス層開発者 | 作業: `WorkflowService` を event delivery dedup 正本フローへリファクタし、再送成功時の HTTP 204 と tx 一貫性を維持する | 制約: Start/Cancel/Publish の既存契約を退行させないこと、例外マッピング契約を維持すること、Controller へ tx ロジックを分散しないこと | 活用: `WorkflowService` の既存 transaction block と projection builder | 要件: Requirement 1, Requirement 3, Requirement 4 | 完了条件: 再送経路が deterministic になり、`AlreadyApplied` で 204 を返し、projection 更新整合が保たれる。着手時は tasks.md の当該タスクを `[-]` に変更し、実装後は `log-implementation` を記録して `[x]` に更新する。_

- [x] 6. rollback 後の補正処理（projection再構築 + event_store insert-skip）を実装する
  - 対象ファイル: `api/Statevia.Core.Api/Services/WorkflowService.cs`, `api/Statevia.Core.Api/Abstractions/Persistence/IEventStoreRepository.cs`, `api/Statevia.Core.Api/Persistence/Repositories/EventStoreRepository.cs`
  - `AlreadyApplied` かつ DB 未反映時に projection を再構築し、event_store は insert-skip で補完する
  - 目的: 「Engine 適用済み・DB未反映」状態を安全に自己修復する
  - _活用: `AppendAsync` 実装、既存 projection 更新 API_
  - _要件: Requirement 1, Requirement 3_
  - _Prompt: spec `o6-subtickets-manufacturing` のタスクを実装してください。最初に `spec-workflow-guide` を実行し、ワークフローを確認してから着手してください。役割: トランザクション永続化に強い信頼性エンジニア | 作業: `AlreadyApplied` 時の補正フローとして projection 再構築と event_store の insert-skip 補完を実装する | 制約: event_store の append-only 前提を守ること、破壊的更新をしないこと、再試行時も冪等に動作すること | 活用: `EventStoreRepository` の append 実装と workflow projection 更新 API | 要件: Requirement 1, Requirement 3 | 完了条件: 補正処理を何度実行しても二重書き込みせず、DB 状態が engine 状態へ収束する。着手時は tasks.md の当該タスクを `[-]` に変更し、実装後は `log-implementation` を記録して `[x]` に更新する。_

- [x] 7. リトライポリシーとログ必須キーを実装する
  - 対象ファイル: `api/Statevia.Core.Api/Services/*`, `api/Statevia.Core.Api/Configuration/EventDeliveryRetryOptions.cs`, `api/Statevia.Core.Api/Infrastructure/EventDeliveryRetryPolicy.cs`, `api/Statevia.Core.Api/Program.cs`, `api/Statevia.Core.Api.Tests/*`
  - `maxAttempts=3` の段階的バックオフ（jitter有効）を導入し、`timeout` は再試行対象外にする。必須ログキーを出力する
  - 目的: 運用要件（再試行制御・観測性）を満たす
  - _活用: 既存 logging 設計（STV-403/404）、Options パターン_
  - _要件: Requirement 3, Requirement 6_
  - _Prompt: spec `o6-subtickets-manufacturing` のタスクを実装してください。最初に `spec-workflow-guide` を実行し、ワークフローを確認してから着手してください。役割: 可観測性とレジリエンスを担当するバックエンドエンジニア | 作業: timeout を除外した最大3回の段階的バックオフ（jitter 有効）を実装し、再送処理の必須ログキー出力を強制する | 制約: リクエストタイムアウト予算を超えないこと、無制限リトライを導入しないこと、既存ログ形式との互換を保つこと | 活用: 既存の request/engine logging 基盤と options バインディング実装 | 要件: Requirement 3, Requirement 6 | 完了条件: リトライ挙動が設定可能かつ上限付きで、timeout は除外され、必須キーがログに出力される。着手時は tasks.md の当該タスクを `[-]` に変更し、実装後は `log-implementation` を記録して `[x]` に更新する。_

- [x] 8. API/Engine 統合テストと契約ドキュメントを更新する
  - 対象ファイル: `api/Statevia.Core.Api.Tests/*`, `engine/Statevia.Core.Engine.Tests/*`, `docs/statevia-data-integration-contract.md`, `docs/core-api-interface.md`, `.workspace-docs/50_tasks/10_in-progress/v2-ticket-backlog.md`
  - 再送・重複・rollback・再起動喪失時422のケースをテストで担保し、契約ドキュメントとバックログの実装状態を更新する
  - 目的: STV-413〜418 の受け入れを実証し、トレーサビリティを完了させる
  - _活用: 既存 API 統合テスト、event_store/WorkflowService テスト資産_
  - _要件: Requirement 2, Requirement 4, Requirement 5, Requirement 6_
  - _Prompt: spec `o6-subtickets-manufacturing` のタスクを実装してください。最初に `spec-workflow-guide` を実行し、ワークフローを確認してから着手してください。役割: 契約とテストに強い QA 志向のフルスタック保守者 | 作業: 再送冪等・rollback 補正・再起動喪失時 422 のケースをテスト追加/拡張し、docs/backlog を実装状態に合わせて更新する | 制約: 既存アサーションを弱めないこと、実装と一致しない記述を docs に書かないこと、未定義の契約差分を増やさないこと | 活用: 既存統合テスト群と STV-413〜418 仕様反映済みの契約ドキュメント | 要件: Requirement 2, Requirement 4, Requirement 5, Requirement 6 | 完了条件: 重要フローのテストがすべて green で、docs/backlog が実装状態を正確に反映する。着手時は tasks.md の当該タスクを `[-]` に変更し、実装後は `log-implementation` を記録して `[x]` に更新する。_
