# Requirements: O6 サブチケット製造フェーズ（STV-413〜STV-418）

## Introduction

`STV-413`〜`STV-418` は仕様化フェーズを完了したため、次は Core-API / Engine / ドキュメントの**製造（実装）フェーズ**へ移行する。  
本 spec では、`o6-subtickets_detailed_spec.md` で固定した契約を、実コード・テスト・運用手順へ落とし込むための要件を定義する。

## Alignment with Product Vision

O6 の未確定懸念（C2/C7/C11/C13/C14）を実装まで閉じることで、イベント順序・永続化・Read Model 一貫性に関する運用リスクを低減し、Statevia の「契約駆動で追跡可能な実行基盤」という目的に沿う。

## Requirements

### Requirement 1 — STV-413 実装（projection 更新トランザクションの実装固定）

**User Story:** As an **API 保守者**, I want **コマンド処理時の event_store / reducer / projection 更新順序がコードで一貫して実装される** so that **仕様と実装の乖離を防げる**。

#### Acceptance Criteria — Requirement 1

1. WHEN `POST /v1/workflows` / `POST /v1/workflows/{id}/cancel` / `POST /v1/workflows/{id}/events` を処理する THEN Core-API SHALL **同一トランザクション内**で event 永続化と projection 更新を完了する。
2. WHEN 書き込み途中で失敗する THEN Core-API SHALL **部分更新を残さずロールバック**する。
3. WHEN 実装レビューする THEN トランザクション境界と更新順序を **コード上で追跡可能**（サービス層または永続化層）である。

### Requirement 2 — STV-414 実装（event_store 対応表との整合）

**User Story:** As a **運用者**, I want **`EventStoreEventType` と永続 payload の対応がコードとテストで検証される** so that **監査ログとイベント語彙の整合を維持できる**。

#### Acceptance Criteria — Requirement 2

1. WHEN 各 HTTP 契機（Start/Cancel/Publish）を実行する THEN event_store SHALL 正しい `type` と payload で記録される。
2. WHEN 新規イベント種別の追加余地を扱う THEN 実装 SHALL 既存 3 種を壊さず拡張可能な形を維持する。
3. WHEN テストを実行する THEN event_store レコードの `type` / payload 主要項目を検証するケースが存在する。

### Requirement 3 — STV-415 実装（再送べき等とリトライ）

**User Story:** As an **SRE**, I want **再送時の重複排除と失敗時リトライの挙動が実装される** so that **二重適用や無限リトライを防げる**。

#### Acceptance Criteria — Requirement 3

1. WHEN 同一イベントを再送する THEN システム SHALL `clientEventId`（または同等キー）で重複を検知し二重適用しない。
2. WHEN バッチ処理が失敗する THEN システム SHALL バッチ単位でロールバックし、再試行時もべき等を維持する。
3. WHEN リトライ上限を超える THEN システム SHALL 構造化ログへ失敗を記録し、運用が手動介入できる状態にする。

### Requirement 4 — STV-416 実装（GetSnapshot と DB projection の責務固定）

**User Story:** As an **API 利用者**, I want **HTTP Read の正が DB projection であることがコード・テストで担保される** so that **Engine メモリ状態と誤解しない**。

#### Acceptance Criteria — Requirement 4

1. WHEN `GET /v1/workflows/{id}` および `GET /v1/workflows/{id}/graph` を呼ぶ THEN API SHALL DB projection を返す。
2. WHEN Engine in-memory 状態と DB が一時的に差分を持つ可能性がある THEN 実装・コメント SHALL Read 経路で DB を正とする設計を明示する。
3. WHEN 回帰テストを実行する THEN Read API が Engine 直接参照に依存しないことを検証できる。

### Requirement 5 — STV-417 実装（nodes 未対応要素の段階導入）

**User Story:** As a **定義作者**, I want **`onError` / `timeout` / `output` / `controls` の導入順が実装計画とテストに反映される** so that **MVP 互換を保ちながら拡張できる**。

#### Acceptance Criteria — Requirement 5

1. WHEN 現行 MVP 範囲外フィールドを含む nodes 定義を登録する THEN API SHALL 明示的なエラーを返す。
2. WHEN 段階導入を開始する THEN 優先順位（P1〜P4）に沿った実装チケットへ分解される。
3. WHEN 仕様更新する THEN `v2-nodes-to-states-conversion-spec.md` と実装状態の差分が追跡できる。

### Requirement 6 — STV-418 実装（横断トレーサビリティ）

**User Story:** As a **PM**, I want **実装タスク・仕様・完了記録が相互参照で追跡できる** so that **O6 クローズ判定を再現可能にする**。

#### Acceptance Criteria — Requirement 6

1. WHEN 製造フェーズの各タスクを完了する THEN backlog / spec-workflow / 関連 docs SHALL 完了状態と反映先を一致させる。
2. WHEN 後続メンテナが参照する THEN STV-413〜418 の実装状況を単一導線で把握できる。
3. WHEN O6 をクローズする THEN 未実装項目は具体的な後続チケットへ移管済みである。

## Non-Functional Requirements

### Code Architecture and Modularity

- トランザクション境界はサービス層で一貫管理し、Repository 呼び出し順が追跡可能であること。
- event_store 更新と projection 更新の責務を分離し、テストで結合点を保証すること。

### Reliability

- 失敗時のロールバックと再試行が deterministic に再現できること。
- べき等キー未指定時の挙動を明示し、運用上の事故を防ぐこと。

### Observability

- 再送・重複排除・上限超過のログに workflowId / tenantId / traceId 相当の相関情報を含めること。

### Verification

- 変更後は該当プロジェクトで `dotnet test` を実行し、回帰がないことを確認する。

## References

- `.workspace-docs/30_specs/10_in-progress/o6-subtickets_detailed_spec.md`
- `.workspace-docs/50_tasks/10_in-progress/v2-ticket-backlog.md`
- `docs/statevia-data-integration-contract.md`
- `AGENTS.md`
