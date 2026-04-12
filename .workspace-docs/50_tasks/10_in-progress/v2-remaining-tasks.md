# 残り課題・タスク一覧

- Version: 1.0.2
- 更新日: 2026-04-12
- 対象: Statevia v2 残タスク（R 系リファクタ・P0～P4・O 系）
- 関連: `.workspace-docs/40_plans/10_in-progress/v2-modification-plan.md`, `.workspace-docs/50_tasks/10_in-progress/v2-ticket-backlog.md`, `.workspace-docs/50_tasks/10_in-progress/v2-logging-v1-tasks.md`, `.workspace-docs/50_tasks/10_in-progress/v2-input-future-backlog.md`

---

本ドキュメントは、Statevia v2 の**残り課題**を整理し、**Core-API のリファクタリング**を含めてタスクに分割した一覧です。  
内容は **`.workspace-docs/` 配下の計画・仕様** および **`docs/` 配下の契約ドキュメント**（`core-api-interface.md` など）を前提としています。

> 配置ポリシー: 本ファイルはタスク管理の正本として `.workspace-docs/50_tasks/10_in-progress/` で管理する。関連する仕様は `.workspace-docs/30_specs/`、計画は `.workspace-docs/40_plans/` を参照する。

---

## 現在の仕分け（2026-04-12）

- **完了**: `R1`～`R7`、`0.1`～`0.4`、`2.1`～`2.9`、`3.1`～`3.3`、`4.1`～`4.5`、`O1`～`O6`
- **未完了**: なし（実行チケットの正本は `v2-ticket-backlog.md` に従う）
- **見送り（今はしない）**: `O7`（認証機能追加時に着手。チケット `STV-411`）。あわせて `STV-412`（ユーザー定義マスキング・外部テンプレート）はポストリリース扱い
- **棄却**: なし

補足:

- **O6** は `STV-410`（分解）および `STV-413`～`STV-418`（C2/C7/C11/C13/C14 の仕様化・ロードマップ統合）で**計画スコープは完了**。**U1 案 C のコールバック本線**など、詳細仕様の「将来」節に残る実装は別エピック（`o6-subtickets_detailed_spec.md`）。
- 実行チケットの主管理は `v2-ticket-backlog.md`（`STV-401` 以降。未完了チケットは現状なし）。
- Logging の実装粒度は `v2-logging-v1-tasks.md`（`LOG-1`～`LOG-7`）を参照する。

---

## 凡例

| 記号       | 意味                                                   |
| ---------- | ------------------------------------------------------ |
| **P0～P4** | 改修タスクのフェーズ（アーキテクチャ計画における段階） |
| **R**      | Core-API リファクタリング関連                          |
| **O**      | その他・今後の課題                                     |

---

## 1. Core-API リファクタリング（R 系タスク）

modification-plan 8.3「今後の課題」の **Core-API のリファクタリング** をサブタスクに分割したもの。実施タイミングは P2 前後〜P4 で、既存機能を壊さない範囲で並行または段階的に実施可能。

対象: R 系（Core-API リファクタリング）

| ID | 状態 | タスク | 優先度 | 依存 | 完了条件 | 備考 |
|----|------|--------|--------|------|----------|------|
| **R1** | 完了 | Controller の責務整理 | - | - | Controller を HTTP 入出力に限定し、検証・バインディングは ASP.NET 標準、ビジネスロジックは Service へ委譲 | - |
| **R2** | 完了 | Service レイヤーの導入・明確化 | - | - | 永続化・Engine 呼び出しを Service に集約し、`IDefinitionService` / `IWorkflowService` 等を UseCase 単位で整理 | - |
| **R3** | 完了 | Repository の責務分割 | - | - | DbContext は Repository 内に閉じ、Service は `IDefinitionRepository` 等のインターフェースのみに依存 | - |
| **R4** | 完了 | 共通化（エラーハンドリング・マッピング） | - | 2.6 と連携可 | 例外→HTTP を一箇所に集約し、data-integration-contract §7 形式の共通 DTO とフィルター等を導入 | - |
| **R5** | 完了 | 共通化（ID 解決） | - | - | display_id ⇔ UUID を `DisplayIdService` に一本化し重複解決を削除 | - |
| **R6** | 完了 | DI とテスト容易性 | - | R2, R3 | コンストラクタインジェクション徹底、インターフェース差し替えで単体テストが可能 | - |
| **R7** | 完了 | アーキテクチャ文書の更新 | - | R1～R6 | レイヤー・DI・永続化方針を AGENTS.md 等に反映 | - |

**実施順序の目安**: R1 → R2, R3（並行可）→ R4, R5 → R6 → R7。P2 の 2.6（エラー写像）と R4 は同時に扱うと効率的。

---

## 2. V2 改修タスク（P0～P4）の参照一覧

詳細なタスク内容は **`.workspace-docs/40_plans/10_in-progress/v2-modification-plan.md`** と各種 v2 仕様（`architecture.v2.md`, `scheme.v2.md`, `core-engine-*.md`, `core-api-interface.md` 等）に記載されています。  
ここでは、実施の単位感を揃えるために代表的なタスク名のみを一覧化します。

### フェーズ0: 契約・スキーマの確定（契約と DB ベースライン）

対象: フェーズ0（契約・スキーマ）

| ID | 状態 | タスク | 優先度 | 依存 | 完了条件 | 備考 |
|----|------|--------|--------|------|----------|------|
| 0.1 | 完了 | Execution Read Model 型の定義 | - | - | - | - |
| 0.2 | 完了 | contracts ディレクトリ（任意）— `api/Statevia.Core.Api/Contracts/` に HTTP 契約 DTO・エラー型等を配置 | - | - | - | - |
| 0.3 | 完了 | command_dedup マイグレーション | - | - | - | - |
| 0.4 | 完了 | 冪等キー設計の文書化 | - | - | - | - |

### フェーズ1: Core Engine（C#）独立サービス（エンジン本体）

対象: フェーズ1（Core Engine）

| ID | 状態 | タスク | 優先度 | 依存 | 完了条件 | 備考 |
|----|------|--------|--------|------|----------|------|
| 1.1～1.9 | 完了 | Core Engine プロジェクト構成、Domain 型・イベント・Reducer、Decide UseCase、Guards、HTTP Adapter、テスト、ヘルス | - | - | - | - |

### フェーズ2: Core-API の改修（書き込みフロー・冪等・エラー）

対象: フェーズ2（Core-API）

| ID | 状態 | タスク | 優先度 | 依存 | 完了条件 | 備考 |
|----|------|--------|--------|------|----------|------|
| 2.1 | 完了 | Core-API から Engine への in-process 呼び出し（`IWorkflowEngine` / `WorkflowService`）と `event_store` の DB スキーマ・EF マッピング・**コマンド由来の追記**（`IEventStoreRepository`：`WorkflowStarted` / `WorkflowCancelled` / `EventPublished` 等、U1, U2）。 | - | - | - | - |
| 2.2 | 完了 | display_ids / GET の {id} / POST /v1/workflows の ID 仕様適用（U3, U4） | - | - | - | - |
| 2.3 | 完了 | Write フローを RPC + 永続化に変更 | - | - | - | - |
| 2.4 | 完了 | command_dedup の導入 | - | - | - | - |
| 2.5 | 完了 | tenant_id の扱い — X-Tenant-Id 任意ヘッダ（省略時 `default`）、workflow_definitions / workflows に tenant_id 追加、一覧・詳細・作成・冪等をテナントスコープ | - | - | - | - |
| 2.6 | 完了 | エラー写像の統一 | - | - | - | - |
| 2.7 | 完了 | GET /executions/:id の契約準拠 — `GET /v1/workflows/{id}` が Execution Read Model を返し、`graphId`（definition の displayId）と `nodes[]`（execution_graph_snapshots.graph_json から変換）を埋める | - | - | - | - |
| 2.8 | 完了 | GET /graphs/:graphId（任意）— GET /v1/graphs/{graphId}、compiled_json から nodes/edges を組み立てて返す | - | - | - | - |
| 2.9 | 完了 | idempotency_keys の廃止（冪等は command_dedup に一本化済み。本コードベースに idempotency_keys テーブルは存在しない） | - | - | - | - |

### フェーズ3: UI の改修（契約適合と UX）

対象: フェーズ3（UI）

| ID | 状態 | タスク | 優先度 | 依存 | 完了条件 | 備考 |
|----|------|--------|--------|------|----------|------|
| 3.1 | 完了 | Read Model 型の利用 | - | - | - | - |
| 3.2 | 完了 | 409 エラー表示 | - | - | - | - |
| 3.3 | 完了 | GET /graphs/:graphId の利用（`useGraphDefinition` → Core-API、失敗時は `graphs/registry` にフォールバック） | - | - | - | - |

**UI 残タスクの整理（`services/ui` と Core-API の照合）** — 下表は**補助表**（区分・状態・内容の整理用）。標準7列タスク表には従わない。

| 区分                  | 状態         | 内容                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| --------------------- | ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **P3 表の 3.1 / 3.2** | 完了マーク済 | Read Model 型・409 トースト（`lib/errors.ts`）は利用中。                                                                                                                                                                                                                                                                                                                                                                                                                 |
| **3.3**               | **完了**     | `app/api/core/[...path]/route.ts` で `graphs` → `v1/graphs` をプロキシ。`useGraphDefinition` が `GET /graphs/:graphId` を優先し、404/空時は `graphs/registry` にフォールバック。                                                                                                                                                                                                                                                                                        |
| **O4（UI 側）**       | **API 対応済** | `WorkflowsController`: `GET …/state`（`WorkflowViewDto`）、`GET …/events`（event_store）、`GET …/stream`（グラフ変化を SSE）、`POST …/nodes/{nodeId}/resume`（PublishEvent と同等）。プロキシは `workflows` → `v1/workflows`。 |
| **O3（UI 波及）**     | **完了** | Core-API は camelCase 統一済み。UI は `types` / `workflowView` / `mapGraphDefinitionResponse` から PascalCase フォールバックを削除済み。                                                                                                                                                                                                                                                                                                                                                                |
| **O1 / O2**           | **API 対応** | `GET /v1/workflows?limit=&offset=&status=` / `GET /v1/definitions?limit=&offset=&name=` で `PagedResult<T>`。クエリなしは従来どおり配列。                                                                                                                                                                                                                                                                                                                                  |
| **P4.1 / 4.2**        | **完了**     | `CORE_API_E2E_URL` 指定時に `core-api-real.spec.ts`（Cancel・冪等・409 API）と `core-api-ui-workflow.spec.ts`（Cancel 成功・409 UI）を実行。詳細は `v2-ticket-backlog.md`（`STV-401`/`STV-402`）および `../20_done/v2-e2e-cancel-idempotency_backlog.md`。                                                                                                                                                                                                                                                                                |

### フェーズ4: クリーンアップ・検証（E2E・運用整備）

対象: フェーズ4（クリーンアップ・検証）

| ID | 状態 | タスク | 優先度 | 依存 | 完了条件 | 備考 |
|----|------|--------|--------|------|----------|------|
| 4.1 | 完了 | E2E テスト（Cancel シーケンス） | P1 | - | Core-API 実体で Cancel シナリオが green、`cancelled`（または契約上の終端）を検証 | `STV-401` → `../20_done/v2-e2e-cancel-idempotency_backlog.md` |
| 4.2 | 完了 | E2E テスト（冪等・409） | P1 | 4.1 | 冪等・409 の契約・UI 表示を E2E で回帰可能にする | `STV-402` → 同上 |
| 4.3 | 完了 | 既存 core（C#）の扱いの決定（v2 は C# Core-API のみ、TypeScript Core-API は `legacy/core-api-ts` タグで保管） | - | - | - | - |
| 4.4 | 完了 | AGENTS.md の更新 | - | - | - | - |
| 4.5 | 完了 | 運用 Docker 構成の文書化（`docs/operations-docker.md`） | - | - | - | - |

---

## 3. その他・今後の課題（O 系タスク）

もともと modification-plan §8.2（懸念）・§8.3（今後の課題）から抽出した区分。**2026-04-12**: §8.3 の項目の多くは **O1〜O5** で完了済み（計画書側も同期）。**冪等レスポンス再利用**や **U1 案 C 本線**などの拡張は `v2-modification-plan.md` §8.3 表の末尾を参照。

対象: O 系（その他・今後）

| ID | 状態 | タスク | 優先度 | 依存 | 完了条件 | 備考 |
|----|------|--------|--------|------|----------|------|
| **O1** | 完了 | 一覧 API のページング | - | - | `?limit=&offset=` と `PagedResult<T>`（`items`, `totalCount`, `hasMore` 等）。クエリなしは従来の配列。 | `docs/core-api-interface.md` |
| **O2** | 完了 | 一覧 API のフィルタリング | - | - | `status`（workflow 完全一致）・`name`（definition 部分一致）。 | `docs/core-api-interface.md` |
| **O3** | 完了 | API レスポンスの camelCase 統一 | - | - | Core-API は `AddJsonOptions` で camelCase。UI は PascalCase フォールバックを削除済み。 | - |
| **O4** | 完了 | UI の C# API レスポンス完全適合 | - | - | `GET /v1/workflows/{id}` は `WorkflowResponse`。`state` / `events` / `stream` / `resume` も実装済み。 | - |
| **O5** | 完了 | アーキテクチャの検討 | - | - | R1〜R7 と `AGENTS.md` に運用方針を反映済み。 | - |
| **O6** | 完了 | 懸念の解消（仕様・追跡） | - | - | `STV-410` で分解、`STV-413`～`STV-418` で C2/C7/C11/C13/C14 を仕様化しロードマップを統合。反映先は `docs/statevia-data-integration-contract.md` / `AGENTS.md` 等。 | U1 案 C **本線実装**は別エピック。`o6-subtickets_detailed_spec.md`、2026-04-11 補足（422 / `event_delivery_dedup`）は `v2-ticket-backlog.md` 仕分け参照 |
| **O7** | 見送り | テナント ID の管理機能 | P3 | 認証エピック | X-Tenant-Id / `tenant_id` 以外のテナント管理・認可を認証と同時スコープで設計・実装。 | 現状はスコープのみ（2.5）。`STV-411` |

**冪等（Idempotency Key）**: V2 では **command_dedup**（P0: 0.3, 0.4 / P2: 2.4, 2.9）で別タスクとして扱う。旧 idempotency_keys からの移行は v2 計画・スキーマに含まれる。

---

## 4. タスクの依存関係（簡易図）

```text
[V2 改修]  P0 → P1 → P2 → P3 → P4   （v2/modification-plan に従う）

[Core-API リファクタ R]
  R1 ──┬── R2 ──┬── R6 ── R7
       └── R3 ──┘
  R4（2.6 と連携）、R5 は P2 前後で実施可能

[その他 O]
  O1～O6 は v2 計画スコープで完了（2026-04-12 時点）
  O7（テナント ID 管理）は認証導入時に O7 とまとめて対応（STV-411）
```

---

## 5. 実施順序の推奨（全体）

1. **V2 フェーズ0**（0.1, 0.3, 0.4）— 型・DB・冪等設計
2. **V2 フェーズ1**（1.1～1.9）— Core Engine 実装
3. **V2 フェーズ2**（2.1, 2.2, 2.4, 2.5, 2.6）— RPC・basis・dedup・tenant・エラー
   - **並行**: **R1, R2, R3**（Core-API 責務分割）→ **R4, R5**（共通化）→ **R6, R7**
4. **2.3**（Write フロー差し替え）
5. **2.7, 2.8, 2.9**
6. **V2 フェーズ3**（3.1～3.3）— UI
7. **V2 フェーズ4**（4.1～4.5）— E2E・ドキュメント・運用
8. **O1～O6** — **完了**（O6 は STV-410〜418。コールバック本線など将来実装は詳細仕様へ）
9. **O7**（テナント ID 管理）— **認証機能追加時に併せて対応**（`STV-411`）

---

## 6. 参照ドキュメント

| ドキュメント                                | 内容                                       |
| ------------------------------------------- | ------------------------------------------ |
| `.workspace-docs/50_tasks/20_done/v2-input-implementation-tasks.md` | Workflow **input / output**・states の `input` の実装タスク分割（IO-1〜） |
| `.workspace-docs/50_tasks/10_in-progress/v2-input-future-backlog.md` | `input` 機能の将来積み残し（`$.context` / `$.env` など） |
| `.workspace-docs/50_tasks/10_in-progress/v2-logging-v1-tasks.md` | API / Engine のコンソールログ項目と v1 実装タスク（LOG-1〜） |
| `.workspace-docs/50_tasks/10_in-progress/v2-ticket-backlog.md` | 実装チケット一覧（**未完了チケットなし**。見送り: `STV-411` / `STV-412`） |
| `.workspace-docs/40_plans/10_in-progress/v2-modification-plan.md` | 改修計画・懸念・今後の課題（8.2, 8.3）     |
| `docs/core-api-interface.md`                | Core-API HTTP 契約                         |
| `docs/data-integration-contract.md`         | データ連携契約（Read Model・エラー形式等） |
| `AGENTS.md`                                 | アーキテクチャ概要・起動方法・テスト       |

---

## 変更履歴

| 版 | 日付 | 内容 |
|----|------|------|
| 1.0.2 | 2026-04-12 | `v2-ticket-backlog.md` と同期。**O6** を完了に更新（`STV-410`〜`418`）。仕分け・依存図・参照表・手順 8 を追随。 |
| 1.0.1 | 2026-04-03 | `4.1`/`4.2`（`STV-401`/`STV-402`）を完了に更新。E2E 補助表を現状に合わせた。 |
| 1.0.0 | 2026-04-02 | メタブロック・タスク表7列化（R/P/O/フェーズ）。UI 残タスクは補助表として注記。 |
