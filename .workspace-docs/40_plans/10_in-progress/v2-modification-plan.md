# Statevia v2 改修計画（main ブランチからの移行）

- Version: 1.0.2
- 更新日: 2026-04-12
- 対象: main ブランチから v2 への移行（C# Core-API / Engine のみ）
- 関連: `.workspace-docs/30_specs/`、`.workspace-docs/50_tasks/`

---

本ドキュメントは **`.workspace-docs/30_specs/` の仕様のみ** を参照し、main ブランチから v2 に移行するための改修計画です。  
**Core-API（TypeScript）は使わず、Core（C#）を Core-Engine（C#）と Core-API（C#）に分離** します。  
（`docs/*.v2.md` は参照していません。）

> 配置ポリシー: 本ファイルは計画ドキュメントの正本として `.workspace-docs/40_plans/10_in-progress/` で管理する。関連する実装タスクは `.workspace-docs/50_tasks/`、確定仕様は `.workspace-docs/30_specs/` を参照する。

---

## 1. v2 の前提（.workspace-docs/30_specs のみより）

### 1.1 アーキテクチャ（.workspace-docs/30_specs/20_done/v2-architecture.md）

- **3 層**: `UI → Core-API → Core-Engine`
- **Core-API と Core-Engine の関係**: **In-process call**（同一プロセス内・同一言語での呼び出し）
- **DB**: Definitions / Workflow Runs / Graph Snapshot を保持

### 1.2 責務の分離（本計画での解釈）

| レイヤー        | 実装                                  | 責務                                                                                                      |
| --------------- | ------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| **UI**          | 現行のまま（TypeScript / Next.js 等） | ワークフロー編集・グラフ可視化・実行操作。**Core-API（C#）** に HTTP でアクセスする。                     |
| **Core-API**    | **C#**（新規）                        | REST、認証、永続化、**Core-Engine のオーケストレーション**。Controller → Repository → Engine → Database。 |
| **Core-Engine** | **C#**（現行 core から分離）          | Workflow Runtime のみ。FSM、Fork/Join、Wait/Event、Scheduler、ExecutionGraph。HTTP も DB も持たない。     |

### 1.3 Core-Engine（C#）の責務（.workspace-docs/30_specs/20_done/v2-core-engine-spec.md）

- **公開 API（ライブラリ）**:  
  `Start(definition)` / `PublishEvent(eventName)` / `CancelAsync(workflowId)` / `GetSnapshot(workflowId)` / `ExportExecutionGraph(workflowId)`
- コンポーネント: WorkflowInstance, FSM, JoinTracker, Scheduler, StateExecutor, EventProvider, ExecutionGraph
- 定義の読み込み・検証・コンパイル（CompiledWorkflowDefinition の生成）は Engine が担当可能

### 1.4 Core-API（C#）の責務（.workspace-docs/30_specs/20_done/v2-core-api-spec.md）

- REST で Engine を公開。**パスには /v1/ プリフィックスを付ける**。
- **エンドポイント**（U4 決定）: パスの **{id}** は **表示用 ID と UUID の両方**を受け付ける（形式で判別）。レスポンスでは **display_id**（表示用 ID）と **resource_id**（UUID）を両方返す。
  - **Definitions**: `POST /v1/definitions`, `GET /v1/definitions/{id}`
  - **Workflow Runs**:  
    `POST /v1/workflows`（body: `definitionId`。レスポンスは display_id と resource_id を返す）,  
    `GET /v1/workflows/{id}`（DB の projection から取得）,  
    `POST /v1/workflows/{id}/cancel`,  
    `POST /v1/workflows/{id}/events`（body: `{ "name": "Approve" }`。workflow 単位の PublishEvent(workflowId, eventName) および既存の PublishEvent(eventName) の両方に対応）,  
    `GET /v1/workflows/{id}/graph`（execution_graph_snapshots から取得）
- **API フロー**: Client → Controller → Repository → **Engine** → Database

### 1.5 DB スキーマ（.workspace-docs/30_specs/20_done/v2-db-schema.md）

**決定**: 永続化の役割分担は次のとおり。**event_store** をイベントソース専用に用意し、**workflow_events** は監査用、**workflows** / **execution_graph_snapshots** は projection（読み取り用テーブル）とする。definitionId / workflowId は **UUID** とし、表示用の独自 ID（英数字 **10 桁**・U3 決定）を**専用テーブル**で管理し、API では表示用 ID から UUID に変換して各テーブルにアクセスする。REST API には **/v1/** プリフィックスを付ける。

- **ID 管理**: 表示用 ID（英数字 **10 桁**）⇔ UUID の対応を専用テーブルで管理。definition / workflow ともに UUID を PK とし、表示・URL 用に短い ID を別管理。
- **workflow_definitions**: id (UUID), name, source_yaml, compiled_json, created_at
- **workflows**: id (UUID), definition_id, status, started_at, updated_at, cancel_requested
- **event_store**: イベントソース専用（workflow_id, seq, type, payload 等）
- **workflow_events**: 監査用（id, workflow_id, seq, type, payload_json, created_at）
- **execution_graph_snapshots**: projection（workflow_id, graph_json, updated_at）

### 1.6 定義形式（.workspace-docs/30_specs/20_done/v2-workflow-definition-spec.md）

- **workflow**: name, initialState
- **states**: StateName → on / wait / join（action / wait / join 型）
- YAML で定義し、Core-Engine が実行

---

## 2. main ブランチの現状と方針

### 2.1 Core（C#）— 現状

- **場所**: ~~`core/`~~ 削除済み。v2 では `engine/`（Statevia.Core.Engine 等）を使用。
- **プロジェクト**: Statevia.Core（ライブラリ）, Statevia.Cli（CLI）, Tests, samples
- **内容**: WorkflowDefinition（workflow + states）、FSM、JoinTracker、ExecutionGraph、WorkflowEngine、StateWorkflowDefinitionLoader/Validator/Compiler、IWorkflowEngine（Start / PublishEvent / CancelAsync / GetSnapshot / ExportExecutionGraph）
- **方針**: これを **Core-Engine（C#）** と **Core-API（C#）** に分ける。TypeScript の Core-API は **使わない**。

### 2.2 Core-API（TypeScript）— 廃止

- **場所**: `services/core-api/`
- **方針**: v2 では **使用しない**。REST と永続化は **Core-API（C#）** に移行したうえで、**当該ディレクトリは削除する**。削除前に **legacy ブランチまたはタグ**で Git に保存する。本計画では「v2 スタックは C# のみ」とする。

### 2.3 UI

- **方針**: 現行 UI はそのまま利用するが、呼び出し先を **Core-API（C#）** のベース URL に変更する。エンドポイントは v2 仕様（/definitions, /workflows）に合わせる。

---

## 3. リポジトリ・ソリューション構成（目標）

**決定**: engine/ と api/ はリポジトリ直下に配置し、別ソリューション（リポジトリ直下にソリューションファイルを置かない）として独立させる。api/ は engine を参照する（将来的には NuGet 等でパッケージとして取り込む）。Statevia.Core は **Statevia.Core.Engine にリネーム**する。

```text
engine/                         # リポジトリ直下、独立ソリューション
├── Statevia.Core.Engine/       # 旧 Statevia.Core をリネーム（プロジェクト名・名前空間とも）
│   ├── Engine, FSM, Join, Scheduler, Execution, Definition など
│   └── IWorkflowEngine, Start, PublishEvent, CancelAsync, GetSnapshot, ExportExecutionGraph
├── Statevia.Core.Engine.Tests/
└── statevia-engine.sln         # engine 用 .sln（U5 決定）

api/                            # リポジトリ直下、独立ソリューション
├── Statevia.Core.Api/          # 新規（ASP.NET Core）。engine へは ../../engine/Statevia.Core.Engine/Statevia.Core.Engine.csproj で ProjectReference（U5 決定）
│   ├── Controllers/             # Definitions, Workflows
│   ├── Persistence/             # workflow_definitions, workflows, event_store, workflow_events, execution_graph_snapshots
│   ├── Hosting/                 # Engine の保持・オーケストレーション
│   └── Program.cs
├── Statevia.Core.Api.Tests/
└── statevia-api.sln            # api 用 .sln。CI ではこの .sln をビルド（engine は ProjectReference で一緒にビルド）（U5 決定）
```

- **Core-Engine**: 既存の `Statevia.Core` を **Statevia.Core.Engine** にリネーム（プロジェクト名・名前空間とも）。HTTP 参照・DB 参照・永続化インターフェースは持たず、純粋なエンジンドメインのみ。開発時は api からプロジェクト参照、将来は NuGet パッケージ参照を想定。
- **Core-API**: 新規の ASP.NET Core プロジェクト。engine をプロジェクト参照し、in-process で IWorkflowEngine を呼ぶ。PostgreSQL 用の永続化は **EF Core** で実装し、**EF Core マイグレーション**でスキーマを管理する。

---

## 4. 改修の段階

### Phase 1: Core（C#）を Core-Engine（C#）として明確化する

**目的**: 現行の Statevia.Core を「Engine 専用」のライブラリとして整理し、Core-API（C#）から参照できるようにする。

**決定**: (1.2) Statevia.Core を **Statevia.Core.Engine にリネーム**（プロジェクト名・名前空間とも）。(2.1) 名前空間は `Statevia.Core.Engine` に統一。(2.2) 将来は NuGet パッケージ発行するが、開発時点では **ローカルのプロジェクト参照**とする。(2.3) Engine は永続化などのインターフェースに依存せず、**純粋なエンジンドメインのみ**持たせる。

| #   | タスク                       | 詳細                                                                                                                                                                                                                                                                                                                              |
| --- | ---------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1.1 | プロジェクトのリネーム・配置 | `Statevia.Core` を **`Statevia.Core.Engine` にリネーム**（プロジェクト名・名前空間とも）。**Statevia.Cli** と **Statevia.Cli.Tests** を engine/ に移し、statevia-engine.sln に含める。**samples/hello-statevia** を **engine/samples/hello-statevia** に移す（engine 参照のみ。.sln には含めない）。CLI や Tests の参照先を更新。 |
| 1.2 | 依存の整理                   | Engine が HTTP・DB・永続化インターフェースに依存していないことを確認。IWorkflowRepository 等は Engine 内に定義せず、API が Engine の入出力だけを永続化する形にする。                                                                                                                                                              |
| 1.3 | パッケージ／名前空間         | 名前空間は **`Statevia.Core.Engine`** に統一。NuGet は将来発行し、開発時は api からプロジェクト参照。                                                                                                                                                                                                                             |

**成果物**: Core-Engine（C#）が単体ライブラリとしてビルドでき、他プロジェクトから参照可能な状態。

---

### Phase 2: Core-API（C#）を新規作成し、Engine を in-process で利用する

**目的**: ASP.NET Core で REST API を実装し、Core-Engine を in-process で呼び出す。

**決定**: (3.1) PostgreSQL アクセスは **EF Core**。(3.2) スキーマは **EF Core マイグレーション**で管理。(3.3) **IWorkflowEngine は DI でシングルトン**。Engine に **PublishEvent(workflowId, eventName)** を追加し、既存の PublishEvent(eventName) も残す。API は両方に対応する。(3.4) **GET /workflows/{id}** は原則 Engine に pull せず **DB から取得**。(3.5) **GET /workflows/{id}/graph** は **execution_graph_snapshots** から取得。(3.6) v2 では**まず認証なし**。認証方式は今後検討して実装。(4.1) **event_store** をイベントソース、**workflow_events** を監査用、**workflows** / **execution_graph_snapshots** を projection として役割を分ける。(4.2) **Engine がイベントを公開し、API がそれを購読**して event_store / projection を更新。(4.3) **再起動ポリシー**を用意し、ユーザー設定に応じて再起動時の挙動（再実行・復元・失効）を制御する。  
**U1 決定**（planning/u1-event-ordering-and-transactions.md より）: イベント公開は **(1) コマンド戻り値** と **(2) コールバック** の**ハイブリッド**。順序保証は**案 C**（Engine が同一 workflow のイベントを直列化し、**順序付きバッチ**でコールバックに渡す）。Fork 時の論理順は**スケジューラの実装に準じる**。**seq は API（DB）が INSERT 時に付与**。監査用 **workflow_events は event_store と同一トランザクション**で INSERT する。

| #   | タスク                             | 詳細                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| --- | ---------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2.1 | プロジェクト作成                   | `Statevia.Core.Api`（ASP.NET Core Web API）を api/ 配下に新規作成。Statevia.Core.Engine をプロジェクト参照。REST には **/v1/** プリフィックスを付ける。                                                                                                                                                                                                                                                                                                                                               |
| 2.2 | Engine のホスティング              | **IWorkflowEngine を DI でシングルトン**登録。cancel / resume を HTTP リクエストをまたいで判別できるようにする。Engine に **PublishEvent(workflowId, eventName)** を追加し、PublishEvent(eventName) も残す。API は両方のエンドポイントを提供。                                                                                                                                                                                                                                                        |
| 2.3 | 定義の永続化                       | `POST /v1/definitions` で name + yaml を受け取り、検証・コンパイルは Engine を利用。結果を DB の workflow_definitions に保存。`GET /v1/definitions/{id}` で取得。definitionId は **UUID**。表示用 ID（英数字 **10 桁**・U3）は専用テーブルで管理。                                                                                                                                                                                                                                                    |
| 2.4 | ワークフロー実行                   | `POST /v1/workflows` で definitionId を受け取り、定義を DB から取得して Engine.Start を呼ぶ。workflowId は **UUID**（表示用 ID は専用テーブルで管理）。**GET /v1/workflows/{id}** は **DB の projection** から返す（原則 Engine には pull しない）。**GET /v1/workflows/{id}/graph** は **execution_graph_snapshots** から取得。`POST /v1/workflows/{id}/cancel` → Engine.CancelAsync。`POST /v1/workflows/{id}/events` → Engine.PublishEvent(workflowId, eventName) または PublishEvent(eventName)。 |
| 2.5 | DB 接続                            | **EF Core** で PostgreSQL に接続。**EF Core マイグレーション**で event_store, workflow_definitions, workflows, workflow_events, execution_graph_snapshots および表示用 ID 管理テーブルを作成。                                                                                                                                                                                                                                                                                                        |
| 2.6 | スナップショット・イベントの永続化 | **Engine がイベントを公開し、API がそれを購読**。(1) コマンド戻り値の Event[] は 1 トランザクションで event_store + workflow_events（監査用）に **同一トランザクション**で INSERT、reducer で projection 更新。(2) コールバックは **案 C**：Engine が順序付きバッチで渡し、API は 1 バッチを 1 トランザクションで event_store + workflow_events に INSERT、reducer で projection 更新。seq は API が INSERT 時に付与。Fork 時の順序はスケジューラに準じる。再起動時は**再起動ポリシー**に従う。       |

**成果物**: Core-API（C#）が立ち上がり、/v1/definitions と /v1/workflows の REST が Core-Engine（C#）を in-process で呼んで動作する状態。

---

### Phase 3: TypeScript Core-API の廃止と UI の切り替え

**目的**: 旧 Core-API（TypeScript）をやめ、UI を Core-API（C#）のみに向ける。

**決定**: (5.1) **services/core-api は削除する**。ただし **legacy ブランチまたはタグ**で一旦 Git に保存する。(6.1) **/executions は削除**し、/workflows のみとする。認証は v2 では入れない。

| #   | タスク                     | 詳細                                                                                                                                                         |
| --- | -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 3.1 | UI の API ベース URL 変更  | 環境変数等で Core-API（C#）の URL を指定し、**/v1/definitions** と **/v1/workflows** を呼ぶように変更。**/executions は削除**し、/workflows のみ利用する。   |
| 3.2 | CORS・認証                 | Core-API（C#）で UI オリジンを許可。v2 では認証なし。認証方式は今後検討。                                                                                    |
| 3.3 | TypeScript Core-API の削除 | **`services/core-api/` は削除**する。削除前に **legacy ブランチまたはタグ**で Git に保存。README / AGENTS.md に v2 では Core-API（C#）のみ使用する旨を明記。 |

**成果物**: UI が Core-API（C#）のみと通信し、/v1/ の REST で動作する状態。

---

### Phase 4: DB と永続化の安定化（任意・当面スキップ）

**目的**: `.workspace-docs/30_specs/20_done/v2-db-schema.md` に完全に合わせ、再起動時の復元や監査を考慮する。

**決定**: (8.1) Phase 4 は**当面スキップ**する。ただし、event_store / projection / 再起動ポリシーなどは**実装する前提**で設計・Phase 2 で考慮する。

| #   | タスク           | 詳細                                                                                                                                                                                              |
| --- | ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 4.1 | スキーマの確定   | event_store（イベントソース）、workflow_events（監査）、workflows / execution_graph_snapshots（projection）の役割は Phase 2 で確定済み。本 Phase では必要に応じてマイグレーション・制約の見直し。 |
| 4.2 | 再起動時の復元   | 再起動ポリシー（再実行・復元・失効）は Phase 2 で用意。本 Phase ではポリシーの拡張や Engine の「状態ロード」API の追加を検討。                                                                    |
| 4.3 | マイグレーション | 本番用のマイグレーションスクリプトとロールバック手順を整備。                                                                                                                                      |

**成果物**: v2 スキーマで一貫した永続化と、必要に応じた復元が可能な状態。

---

### Phase 5: 定義形式の拡張（nodes 形式）（実施）

**目的**: `.workspace-docs/30_specs/20_done/v2-definition-spec.md` の nodes ベース YAML をサポートする。

**決定**: (8.2) Phase 5 は**実施**する。(7.1) nodes 形式は **states ベースの CompiledWorkflowDefinition に変換**する。Engine 内で nodes を直接解釈せず、変換レイヤーを Engine の Definition 側に持ち、実行は既存の states パイプラインに統一する。

| #   | タスク                      | 詳細                                                                                                                                                                                                     |
| --- | --------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 5.1 | nodes → states 変換         | Core-Engine 側で nodes 形式を解釈し、**既存の states ベースの CompiledWorkflowDefinition に変換**する（NodesToStatesConverter 等を Definition レイヤーに追加）。Engine の実行コアは states のまま 1 本。 |
| 5.2 | POST /v1/definitions の拡張 | nodes 形式の YAML を受け付け、変換後に検証・コンパイルして保存。API が nodes / states を判別し、nodes の場合は Engine の変換入りローダーを呼ぶ。                                                         |

**変換仕様の正本**: `.workspace-docs/30_specs/10_in-progress/v2-nodes-to-states-conversion-spec.md`（判別・MVP スコープ・join の `allOf` 決定、`input` は states と同一の `$.` のみ・`${...}` 不採用、明示エラーと無視フィールド）。

**成果物**: nodes 形式のワークフローを登録・実行できる状態。

---

## 5. マイルストーン（目安）

| マイルストーン | 内容                                                                                                           |
| -------------- | -------------------------------------------------------------------------------------------------------------- |
| M1             | Phase 1 完了: Core（C#）が Core-Engine（C#）として整理され、単体でビルド・参照可能。                           |
| M2             | Phase 2 完了: Core-API（C#）が稼働し、/definitions と /workflows の REST が Core-Engine を in-process で使用。 |
| M3             | Phase 3 完了: UI が Core-API（C#）のみを利用。TypeScript Core-API は v2 では使用しないことが明文化されている。 |
| M4             | Phase 4・5（任意）: DB の安定化、nodes 形式のサポート。                                                        |

---

## 6. 参照ドキュメント（.v2.md は含まない）

- `.workspace-docs/30_specs/20_done/v2-architecture.md` — 3 層・責務・データフロー
- `.workspace-docs/30_specs/20_done/v2-core-engine-spec.md` — Engine コンポーネントと API
- `.workspace-docs/30_specs/20_done/v2-core-api-spec.md` — REST エンドポイントと API フロー
- `.workspace-docs/30_specs/20_done/v2-db-schema.md` — テーブル定義
- `.workspace-docs/30_specs/20_done/v2-workflow-definition-spec.md` — states ベースの定義
- `.workspace-docs/30_specs/20_done/v2-definition-spec.md` — nodes ベースの定義（Phase 5 用）
- `.workspace-docs/30_specs/10_in-progress/v2-nodes-to-states-conversion-spec.md` — nodes → states 機械変換（Phase 5 MVP・正本）
- `.workspace-docs/30_specs/20_done/v2-execution-graph-spec.md` — ExecutionGraph の構造
- `.workspace-docs/30_specs/20_done/v2-engine-runtime-spec.md` — ランタイムの振る舞い
- `.workspace-docs/40_plans/10_in-progress/v2-roadmap.md` — Phase 1〜3 の目標
- `docs/definition-spec.md` — main の定義仕様（states）
- `.workspace-docs/50_tasks/20_done/v2-u1-event-ordering-and-transactions.md` — U1 順序保証・トランザクション（ハイブリッド・案 C 等の決定）
- `.workspace-docs/50_tasks/20_done/v2-u2-event-store-schema.md` — U2 event_store スキーマ（案 A 正規化・created_at・workflow_events 最小限の決定）
- `.workspace-docs/50_tasks/20_done/v2-u3-display-id-table.md` — U3 表示用 ID 専用テーブル（1 テーブル display_ids・乱数・62 文字・10 桁・衝突時再生成の決定）
- `.workspace-docs/50_tasks/20_done/v2-u4-get-id-and-response.md` — U4 GET の {id} とレスポンスの ID（パス・レスポンスの id 扱いの議論）
- `.workspace-docs/50_tasks/20_done/v2-u5-api-engine-reference-and-ci.md` — U5 api から engine の参照パスと CI ビルド（ProjectReference・CI 手順の議論）
- `.workspace-docs/50_tasks/20_done/v2-u6-cli-and-samples-placement.md` — U6 Statevia.Cli と samples の配置（engine 配下・CLI は .sln に含める・samples は .sln に含めないの決定）
- `.workspace-docs/50_tasks/20_done/v2-u7-reducer-placement.md` — U7 reducer の所在（Engine に置く・EventEnvelope 型も Engine が定義の決定）
- `.workspace-docs/50_tasks/20_done/v2-u8-restart-policy.md` — U8 再起動ポリシーの具体（デフォルト失効・設定の持ち方・Phase 2 最小実装の議論）
- `.workspace-docs/50_tasks/20_done/v2-u9-publish-event-endpoint.md` — U9 全ワークフロー向け PublishEvent(eventName) の REST（POST /v1/events の議論）
- `.workspace-docs/50_tasks/20_done/v2-u10-nodes-states-discrimination.md` — U10 nodes / states の判別方法（ルートの nodes 配列の有無で判別の議論）
- `.workspace-docs/50_tasks/20_done/v2-u11-legacy-branch-or-tag.md` — U11 TypeScript Core-API 削除時の legacy 保存（ブランチ vs タグの議論）

---

## 7. 決定事項サマリ（open-decisions.md より反映）

| 区分    | 事項                                     | 決定内容                                                                                                                                                                                                                                                                                                                                                   |
| ------- | ---------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1.1     | ディレクトリ配置                         | engine/ と api/ をリポジトリ直下に配置。別ソリューションとして独立。api は engine を参照（将来は NuGet 想定）。                                                                                                                                                                                                                                            |
| 1.2     | Statevia.Core の分離                     | Statevia.Core.Engine にリネーム。                                                                                                                                                                                                                                                                                                                          |
| 2.1     | 名前空間                                 | Statevia.Core.Engine に統一。                                                                                                                                                                                                                                                                                                                              |
| 2.2     | NuGet                                    | 将来発行。開発時はローカル・プロジェクト参照。                                                                                                                                                                                                                                                                                                             |
| 2.3     | 永続化インターフェース                   | Engine は持たない。純粋なエンジンドメインのみ。                                                                                                                                                                                                                                                                                                            |
| 3.1     | PostgreSQL アクセス                      | EF Core。                                                                                                                                                                                                                                                                                                                                                  |
| 3.2     | スキーマ作成                             | EF Core マイグレーション。                                                                                                                                                                                                                                                                                                                                 |
| 3.3     | IWorkflowEngine DI                       | シングルトン。PublishEvent(workflowId, eventName) を追加し、PublishEvent(eventName) も残す。API は両方対応。                                                                                                                                                                                                                                               |
| 3.4     | GET /workflows/{id}                      | 原則 Engine に pull せず DB から取得。                                                                                                                                                                                                                                                                                                                     |
| 3.5     | GET /workflows/{id}/graph                | execution_graph_snapshots から取得。                                                                                                                                                                                                                                                                                                                       |
| 3.6     | 認証                                     | まずは認証なし。今後検討。                                                                                                                                                                                                                                                                                                                                 |
| 4.1     | 永続化の役割                             | event_store＝イベントソース、workflow_events＝監査、workflows / execution_graph_snapshots＝projection。                                                                                                                                                                                                                                                    |
| 4.2     | スナップショット更新                     | Engine がイベントを公開し、API が購読して projection を更新。                                                                                                                                                                                                                                                                                              |
| 4.3     | 再起動時                                 | 再起動ポリシーでユーザー設定に応じて再実行・復元・失効を制御。                                                                                                                                                                                                                                                                                             |
| 5.1     | TypeScript Core-API                      | 削除。legacy ブランチまたはタグで Git に保存。                                                                                                                                                                                                                                                                                                             |
| 6.1     | /executions                              | 削除。/workflows のみ。                                                                                                                                                                                                                                                                                                                                    |
| 7.1     | nodes 形式                               | states ベースの CompiledWorkflowDefinition に変換。                                                                                                                                                                                                                                                                                                        |
| 8.1     | Phase 4                                  | 当面スキップ。実装する前提で考慮。                                                                                                                                                                                                                                                                                                                         |
| 8.2     | Phase 5                                  | 実施。                                                                                                                                                                                                                                                                                                                                                     |
| 9.1     | definitionId                             | UUID。表示用 ID（英数字 **10 桁**・U3）を専用テーブルで管理。                                                                                                                                                                                                                                                                                              |
| 9.2     | workflowId                               | UUID。表示用 ID を同様に管理。                                                                                                                                                                                                                                                                                                                             |
| 9.3     | REST バージョニング                      | /v1/ プリフィックスを付ける。                                                                                                                                                                                                                                                                                                                              |
| U1      | Engine のイベント公開                    | ハイブリッド（コマンド戻り値 + コールバック）。順序保証は**案 C**（順序付きバッチ）。seq は API が INSERT 時に付与。Fork 順はスケジューラに準じる。                                                                                                                                                                                                        |
| U1 付随 | event_store と workflow_events           | 監査用 workflow_events は event_store と**同一トランザクション**で INSERT。                                                                                                                                                                                                                                                                                |
| U2      | event_store のスキーマ                   | **案 A（正規化）**。メタデータを列に持つ。created_at を持つ。workflow_events は**最小限（監査専用）**。詳細は planning/u2-event-store-schema.md および db-schema.md。                                                                                                                                                                                      |
| U3      | 表示用 ID の専用テーブル                 | **1 テーブル（案 A）**。テーブル名 **display_ids**。列は kind, display_id, uuid, created_at。表示用 ID は**乱数**・**62 文字**（0-9, a-z, A-Z）・**10 桁**。衝突時は**再生成**（事前 SELECT せずキー違反で検出）。definition と workflow で display_id は**共有しない**（グローバル UNIQUE）。詳細は planning/u3-display-id-table.md および db-schema.md。 |
| U4      | GET の {id} とレスポンスの ID            | パスの **{id}** は**表示用 ID と UUID の両方**を受け付ける。レスポンスは **display_id**（表示用 ID）と **resource_id**（UUID）を両方返す。一覧 API も同様。POST /v1/workflows のレスポンスも display_id と resource_id を返す。詳細は planning/u4-get-id-and-response.md。                                                                                 |
| U5      | api から engine の参照パス・CI           | ProjectReference は **`../../engine/Statevia.Core.Engine/Statevia.Core.Engine.csproj`**。CI は**案 A**（api の .sln をビルド。engine は ProjectReference で一緒にビルド）。ルートに .sln は**置かない**。.sln 名は **statevia-engine.sln** / **statevia-api.sln**。詳細は planning/u5-api-engine-reference-and-ci.md。                                     |
| U6      | Statevia.Cli と samples の配置           | **CLI** は **engine 配下**。statevia-engine.sln に Statevia.Cli と Statevia.Cli.Tests を含める。**samples** は **engine/samples/hello-statevia**。engine 参照のみ。.sln には含めない。API 利用サンプルは今回スコープ外。詳細は planning/u6-cli-and-samples-placement.md。                                                                                  |
| U7      | reducer の所在                           | **Engine に置く**。EventEnvelope 型と ExecutionState 相当の型も Engine が定義。API は reducer の出力を workflows / execution_graph_snapshots にマッピング。詳細は planning/u7-reducer-placement.md。                                                                                                                                                       |
| U8      | 再起動ポリシーの具体                     | デフォルトは**失効**。設定は **appsettings + 環境変数**。Phase 2 では失効を実装し、再起動時に Running 行の **restart_lost = true** に一括更新（**status は変更しない**。別フラグで管理）。Cancel/Events は 409 または 410。Restore/Replay 指定時は **501 Not Implemented**。詳細は planning/u8-restart-policy.md。                                         |
| U9      | PublishEvent(eventName) のエンドポイント | **POST /v1/events** を用意。body は `{ "name": "<eventName>" }`。200 OK、body は空または `{ "published": true }`。詳細は planning/u9-publish-event-endpoint.md。                                                                                                                                                                                           |
| U10     | nodes / states の判別方法                | **ルートに `nodes` が存在し配列なら nodes 形式、さもなくば states 形式**。両方ある場合は**エラー返却**。不正時もその時点でエラー。詳細は planning/u10-nodes-states-discrimination.md。                                                                                                                                                                     |
| U11     | legacy 保存はブランチかタグか            | **タグ**を採用。タグ名は **legacy/core-api-ts**。削除直前のコミットに annotated タグを打ち push。README / AGENTS.md に参照方法を明記。詳細は planning/u11-legacy-branch-or-tag.md。                                                                                                                                                                        |

---

## 8. 未決定事項・懸念点一覧

計画時点で決まっていない事項、および実装・運用上の懸念点を一覧化する。決まり次第、本文および open-decisions.md に反映すること。

---

### 8.1 未決定事項

| #       | 区分            | 事項                                          | 補足                                                                                                                                                                                                                                                             |
| ------- | --------------- | --------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ~~U1~~  | Engine / 永続化 | **Engine のイベント公開の具体形**             | **決定済み**（planning/u1-event-ordering-and-transactions.md）。ハイブリッド（コマンド戻り値 + コールバック）。案 C（順序付きバッチ）。seq は API が付与。                                                                                                       |
| ~~U2~~  | DB              | **event_store のスキーマ**                    | **決定済み**（planning/u2-event-store-schema.md）。案 A（正規化）。created_at を持つ。workflow_events は最小限（監査専用）。db-schema.md に event_store を追加済み。                                                                                             |
| ~~U3~~  | DB              | **表示用 ID の専用テーブル**                  | **決定済み**（planning/u3-display-id-table.md）。1 テーブル **display_ids**（kind, display_id, uuid, created_at）。乱数・62 文字・**10 桁**・衝突時は再生成（キー違反で検出）。display_id はグローバル UNIQUE。db-schema.md に追加済み。                         |
| ~~U4~~  | API             | **GET の {id} とレスポンスの ID**             | **決定済み**（planning/u4-get-id-and-response.md）。パスは**両方受け付ける**。レスポンスは **display_id**（表示用 ID）と **resource_id**（UUID）。一覧・POST /v1/workflows も同様。                                                                              |
| ~~U5~~  | 構成            | **api から engine の参照パス・CI**            | **決定済み**（planning/u5-api-engine-reference-and-ci.md）。ProjectReference は `../../engine/Statevia.Core.Engine/Statevia.Core.Engine.csproj`。CI は案 A（api の .sln をビルド）。ルートに .sln は置かない。.sln 名は statevia-engine.sln / statevia-api.sln。 |
| ~~U6~~  | 構成            | **Statevia.Cli と samples の配置**            | **決定済み**（planning/u6-cli-and-samples-placement.md）。CLI は engine 配下・statevia-engine.sln に含める。samples は engine/samples/hello-statevia・engine 参照のみ・.sln には含めない。API 利用サンプルはスコープ外。                                         |
| ~~U7~~  | 永続化          | **reducer の所在**                            | **決定済み**（planning/u7-reducer-placement.md）。reducer は Engine に置く。EventEnvelope 型と ExecutionState 相当の型も Engine が定義。API は reducer 出力を workflows / execution_graph_snapshots にマッピング。                                               |
| ~~U8~~  | 永続化          | **再起動ポリシーの具体**                      | **決定済み**（planning/u8-restart-policy.md）。デフォルトは**失効**。再起動時に Running 行の **restart_lost = true** に一括更新（C12 で別フラグに決定）。Restore/Replay は **501**。                                                                             |
| ~~U9~~  | API             | **PublishEvent(eventName) のエンドポイント**  | **決定済み**（planning/u9-publish-event-endpoint.md）。全ワークフロー向けは **POST /v1/events**。body は `{ "name": "<eventName>" }`。200 OK、body は空または `{ "published": true }`。                                                                          |
| ~~U10~~ | Phase 5         | **nodes / states の判別方法**                 | **決定済み**（planning/u10-nodes-states-discrimination.md）。ルートに **`nodes` が存在し配列なら nodes 形式**、さもなくば states 形式。**両方ある場合はエラー返却**。不正時もその時点でエラー。                                                                  |
| ~~U11~~ | 運用            | **legacy 保存はブランチかタグか**             | **決定済み**（planning/u11-legacy-branch-or-tag.md）。**タグ**を採用。タグ名は **legacy/core-api-ts**。削除直前のコミットに annotated タグを打ち push。                                                                                                          |
| ~~U12~~ | DB              | **event_store と workflow_events の二重書き** | **決定済み**。同一トランザクションで event_store と workflow_events の両方に INSERT する（planning/u1-event-ordering-and-transactions.md）。                                                                                                                     |

---

### 8.2 懸念点

Un（8.1）はすべて決定済み。以下 Cn は計画当時の**懸念リストの記録**であり、方針決定・仕様化・実装で解消したものは打ち消し線で示す（**2026-04-12** 時点の同期は §8.2 冒頭メモおよび `v2-remaining-tasks.md` を参照）。**未収束の拡張**は §8.3 に集約する。

**O6 詳細仕様（チケット STV-413〜STV-418）**: `.workspace-docs/30_specs/10_in-progress/o6-subtickets_detailed_spec.md`

**同期メモ（2026-04-12）**: `C2` / `C7` / `C10` / `C11` / `C13` / `C14` は **`STV-413`～`STV-418`** および `docs/statevia-data-integration-contract.md`（§3.3 等）で**仕様化・追跡を完了**。下表は計画当時の区分の**記録**として残す。**U1 案 C のコールバック本線**（順序付きバッチの常時経路）は、現行 in-process 構成では未着手の別エピック。一覧の limit/offset 等の API 実装は `v2-remaining-tasks.md` の **O1/O2** で完了。

| #       | 区分         | 懸念                                          | 補足                                                                                                                                                                                                                                                                               |
| ------- | ------------ | --------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ~~C1~~  | 設計         | **現行 Engine はイベントを返さない**          | **U1 決定**でハイブリッド（コマンド戻り値 + 案 C コールバック）にした。Engine の変更方針は planning/u1-event-ordering-and-transactions.md に沿って実装する。                                                                                                                       |
| ~~C2~~  | 一貫性       | **projection の更新タイミング**               | **仕様化済**（`STV-413`）。案 C 本実装時に一貫適用。現行はコマンド戻り値経路でトランザクション境界を維持。                                                                                                                                                                        |
| ~~C3~~  | ID           | **表示用 ID の衝突リスク**                    | **U3 決定**で 10 桁・62 文字種・衝突時は再生成（キー違反で検出）にした。display_ids でグローバル UNIQUE。懸念は解消。                                                                                                                                                              |
| ~~C4~~  | Phase 4      | **再起動時の Running 扱い**                   | **U8 決定**で失効・再起動時に Running 行の **restart_lost = true** に一括更新（C12 で別フラグに決定）。Restore/Replay は 501。Phase 2 で実装。planning/u8-restart-policy.md。                                                                                                      |
| ~~C5~~  | CI           | **別ソリューション時のビルド**                | **U5 決定**で CI は api の .sln をビルド（engine は ProjectReference で一緒にビルド）。ルートに .sln は置かない。planning/u5-api-engine-reference-and-ci.md。                                                                                                                      |
| ~~C6~~  | UI           | **UI が使う ID**                              | **U4 決定**で API は display_id と resource_id を返す。UI は一覧・詳細・URL に **display_id** を使う方針。Phase 3 で UI 変更を実施するため、懸念は方針決定で解消。                                                                                                                 |
| ~~C7~~  | 仕様         | **EventEnvelope と event_store の対応**       | **仕様化済**（`STV-414`）。対応表は `statevia-data-integration-contract.md` §3.3 を参照。                                                                                                                                                                                          |
| ~~C8~~  | ドキュメント | **表示用 ID の桁数表記の揺れ**                | **10 桁に統一**。planning/u3-display-id-table.md および open-decisions.md の「8 桁」を 10 桁に修正済み。                                                                                                                                                                           |
| ~~C9~~  | DB           | **workflow_definitions / workflows の PK 型** | **uuid 型に統一**。db-schema.md の workflow_definitions / workflows / workflow_events / execution_graph_snapshots の id および FK を **uuid** 型に修正済み。                                                                                                                       |
| ~~C10~~ | API          | **一覧 API のページング**                     | **実装済**（`?limit` / `?offset` と `PagedResult<T>`。`v2-remaining-tasks.md` O1）。                                                                                                                                                                                                |
| ~~C11~~ | 永続化       | **コールバック失敗時のリトライ・再送**        | **仕様化済**（`STV-415`）。再送・重複排除・観測性は `statevia-data-integration-contract.md` / 実装（例: `event_delivery_dedup`）を参照。案 C 本線は別エピック。                                                                                                                     |
| ~~C12~~ | 永続化       | **Lost の status 値の正式扱い**               | **restart_lost (bool) の別フラグ**で管理することを決定。status は変更せず、workflows に **restart_lost** 列を追加。planning/u8-restart-policy.md および db-schema.md に反映済み。API レスポンスに restart_lost を含める。                                                          |
| ~~C13~~ | Engine       | **GetSnapshot と reducer 出力の関係**         | **方針決定済**（`STV-416`）。Read model の正は DB projection とし、`AGENTS.md`（Read-model authority）に記載。                                                                                                                                                                       |
| ~~C14~~ | Phase 5      | **nodes 変換でカバーしきれない要素**          | **段階導入計画の策定済**（`STV-417`）。`v2-nodes-to-states-conversion-spec.md` §11.1 を参照。                                                                                                                                                                                       |

---

### 8.3 今後の課題（プラン上で残すもの）

**同期メモ（2026-04-12）**: 下表のうち**取り消し線**の項目は `v2-remaining-tasks.md` の **O1～O5** で対応済み。本節は改修計画の記録として残し、**拡張・未収束**のみを追記する。

| 課題                            | 内容                                                                                                                                                                                                                                                                                                     |
| ------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ~~**一覧 API のページング**~~   | **完了**（`O1`）。`docs/core-api-interface.md` を参照。                                                                                                                                                                                                                                                  |
| ~~**一覧 API のフィルタリング**~~ | **完了**（`O2`）。`status` / `name` クエリ。                                                                                                                                                                                                                                                             |
| ~~**Core-API のリファクタリング**~~ | **完了**（R1～R7。`O5` と整合）。                                                                                                                                                                                                                                                                       |
| ~~**アーキテクチャの検討**~~    | **完了**（`O5`。`AGENTS.md` 等に反映）。                                                                                                                                                                                                                                                                |
| ~~**UI の C# API レスポンス適合**~~ | **完了**（`O4`）。`/stream`・`/state`・`/nodes/.../resume` 等は Core-API 実装済み。                                                                                                                                                                                                                       |
| ~~**API レスポンスの camelCase シリアライズ**~~ | **完了**（`O3`）。                                                                                                                                                                                                                                                                                       |
| **Idempotency（レスポンス再利用の本格化）** | `command_dedup` により**コマンド単位の重複抑止**は導入済み（`v2-remaining-tasks.md` の `0.3` / `2.4`）。同一キー再送時に **HTTP ステータス・レスポンス本文をキャッシュして返す**完全な再利用は、`CommandDedupRow` の `request_hash` / `status_code` / `response_body`（コード内 TODO）および仕様の追補が残る。 |
| **U1 案 C（コールバック本線）**   | Engine から Core-API への**順序付きバッチ**常時経路は、計画・契約上は確定だが**別エピックで実装**。現状は in-process のコマンド戻り値経路が主。`o6-subtickets_detailed_spec.md` の「将来」節を参照。                                                                                                    |

---

以上を、**Core-API（TypeScript）は使わず、Core（C#）を Core-Engine（C#）と Core-API（C#）に分けた** v2 改修計画とする。

---

## 変更履歴

| 版 | 日付 | 内容 |
|----|------|------|
| 1.0.2 | 2026-04-12 | `v2-remaining-tasks.md` / `v2-ticket-backlog.md` と同期。8.2 に同期メモ、`C2`/`C7`/`C10`/`C11`/`C13`/`C14` を完了扱いに更新。8.3 を完了項目と拡張課題（Idempotency 本文再利用、U1 案 C）に再編。 |
| 1.0.1 | 2026-04-10 | 8.2 直前に O6 詳細仕様（`o6-subtickets_detailed_spec.md`）への参照を追加。 |
| 1.0.0 | 2026-04-02 | メタブロック整備。§1 見出しのパス表記を `30_specs` に修正。 |
