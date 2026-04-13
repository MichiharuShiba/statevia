# Requirements: UI Playground（Phase 3）

> **承認状態**: **承認済み**（2026-04-12）— 依頼文書: [approval-request.md](./approval-request.md)。P3.0 実装着手済み。

## Introduction

**UI Playground** は、`services/ui`（Next.js）上で **ワークフロー定義（YAML / nodes 形式）の登録**と **Core-API 経由の実行・操作・可視化**を短いフィードバックループで行うための開発者向け UI である。既存の実行ダッシュボード（`app/page.tsx`）は Read・グラフ・タイムラインに強みがあるため、Playground は **定義登録・開始のギャップを埋めつつ**、可能な限り既存フック・コンポーネントを再利用する。

**詳細設計（ワイヤー・API 表・フェーズ分割）**: `.workspace-docs/30_specs/10_in-progress/ui-playground-design.md`

**紐づく計画**: `.workspace-docs/40_plans/10_in-progress/v2-roadmap.md`（Phase 3）、`.workspace-docs/30_specs/10_in-progress/v2-ui-spec.md`

**HTTP 正本**: `docs/core-api-interface.md`、`docs/statevia-data-integration-contract.md`

## Alignment with Product Vision

`steering/product.md` の **UI 利用者**（グラフ・実行状況の視覚化）および **契約の正本**原則に沿う。Playground は定義から実行までを **契約どおりの REST** で完結させ、Read Model の正は **GET**（および DB projection）に従う（`AGENTS.md` Read-model authority）。

## Requirements

### Requirement 1 — 定義の登録（YAML → Core-API）

**User Story:** As a **ワークフロー作者**, I want **ブラウザから YAML を `POST /v1/definitions` で登録できる**こと, so that **手動 curl なしで試行錯誤できる**。

#### Acceptance Criteria — Requirement 1

1. WHEN ユーザーが **名前** と **YAML 本文** を入力し登録を実行する THEN UI SHALL `POST /v1/definitions`（`name`, `yaml`）をプロキシ経由で呼び出す。
2. WHEN Core-API が **201** で `displayId` / `resourceId` を返す THEN UI SHALL 成功を表示し、直後の **Start** で使えるよう `definitionId`（displayId または UUID）を保持する。
3. WHEN Core-API が **400** 等で拒否する THEN UI SHALL エラー本文または `error` オブジェクトを利用者が修正できる形で表示する。
4. IF 現行 API に **validate 専用エンドポイントが無い** THEN 本要件は **登録時の検証に依存**する（将来 `POST /v1/definitions/validate` を追加する場合は別 spec で拡張）。

### Requirement 2 — ワークフロー開始と識別子の可視化

**User Story:** As a **ワークフロー作者**, I want **登録済み定義 ID と任意 input で実行を開始できる**こと, so that **すぐに Read とグラフを確認できる**。

#### Acceptance Criteria — Requirement 2

1. WHEN ユーザーが **definitionId**（および任意 **input**）を指定して Start を実行する THEN UI SHALL `POST /v1/workflows` を呼び出す。
2. WHEN **201 Created** で `displayId` / `resourceId` が返る THEN UI SHALL それらを画面上に表示し、以降の Read / 操作の対象 ID として用いる。
3. WHEN **`X-Idempotency-Key`** を付与できる THEN UI SHALL Start / Cancel / Events / Resume で付与を推奨する（キー生成は既存 UI パターンに合わせる）。

### Requirement 3 — ルーティングと既存ダッシュボードとの共存

**User Story:** As a **開発者**, I want **Playground が専用ルートにあり既存の実行ビューと衝突しない**こと, so that **段階導入とリンク共有がしやすい**。

#### Acceptance Criteria — Requirement 3

1. WHEN Playground MVP が提供される THEN アプリ SHALL **`/playground`**（または同等の単一プレフィックス）配下に主要 UI を配置する。
2. WHEN 実行 ID が確定する THEN UI SHALL **`/playground/run/[displayId]`** のような実行フォーカス画面への遷移またはリンクを提供できるようにする（実装フェーズは `tasks.md` に従う）。
3. WHEN 既存のルート `/`（実行ダッシュボード）が存在する THEN Playground SHALL それを**置換せず**共存する（相互リンクは任意）。

### Requirement 4 — 実行中操作（Cancel / Event / Resume）

**User Story:** As a **デバッガ**, I want **実行に対して Cancel・イベント送信・ノード Resume を UI から行える**こと, so that **コマンドと投影の対応を追える**。

#### Acceptance Criteria — Requirement 4

1. WHEN ユーザーが Cancel を実行する THEN UI SHALL `POST /v1/workflows/{id}/cancel` を呼び、**204** を成功として扱う。
2. WHEN ユーザーがイベント名を送信する THEN UI SHALL `POST /v1/workflows/{id}/events`（body: `name`）を呼ぶ。
3. WHEN ユーザーがノード Resume を実行する THEN UI SHALL `POST /v1/workflows/{id}/nodes/{nodeId}/resume` を呼ぶ。
4. WHEN API が **409** / **422** を返す THEN UI SHALL 既存の `lib/errors` 等の方針に沿って利用者に理由を示す。

### Requirement 5 — Read と可視化（契約準拠）

**User Story:** As a **UI 利用者**, I want **GET で取得した Read Model とグラフが正として表示される**こと, so that **Engine メモリと混同しない**。

#### Acceptance Criteria — Requirement 5

1. WHEN 実行が選択されている THEN UI SHALL `GET /v1/workflows/{id}` および `GET /v1/workflows/{id}/graph`（必要に応じて `GET /v1/graphs/{graphId}`）を用いて状態を表示する。
2. WHEN タイムラインを表示する THEN UI SHALL `GET /v1/workflows/{id}/events`（`afterSeq`, `limit`）に準拠する。
3. WHEN リプレイ UI を提供する THEN UI SHALL `GET /v1/workflows/{id}/state?atSeq=` の契約に従う（近似である旨は既存 UI と同様に扱う）。

### Requirement 6 — SSE（任意）と Read の整合

**User Story:** As a **UI 利用者**, I want **SSE で変化通知を受け取りつつ最終状態は GET で確定できる**こと, so that **ポーリング負荷を下げられる**。

#### Acceptance Criteria — Requirement 6

1. IF UI が `GET /v1/workflows/{id}/stream`（SSE）を利用する THEN 実装 SHALL `docs/ui-push-api-spec.md` / `docs/ui-api-auth-tenant-config.md` のテナント・接続方針に従う。
2. WHEN **`GraphUpdated`** イベントを受信する THEN UI SHALL **デバウンス可能な範囲で** `GET /v1/workflows/{id}` または `…/graph` を再取得し、画面を確定状態に揃える。
3. WHEN SSE が切断される THEN UI SHALL ポーリングまたは手動更新で **GET にフォールバック**できる。

### Requirement 7 — コンポーネント再利用

**User Story:** As a **保守者**, I want **既存のグラフ・タイムライン・ノード詳細を重複実装せず使い回せる**こと, so that **バグ修正が一箇所で済む**。

#### Acceptance Criteria — Requirement 7

1. WHEN Playground が実行ビューを提供する THEN 実装 SHALL 既存の `useExecution` / `useGraphDefinition` / `NodeGraphView` 等を**優先的に再利用**する。
2. WHEN 新規コンポーネントを追加する THEN それは **定義編集・登録・開始**など既存に無い責務に限定する。

## Non-Functional Requirements

### Code Architecture

- Playground 専用コードは `services/ui/app/playground/`（仮）のように**ディレクトリを分離**し、既存 `page.tsx` の責務を肥大化させない。

### Security

- 認証は現行スコープ外（ローカル／開発想定）。公開配置する場合は別エピックで認証・レート制限を定義する。

### Usability

- 初回ユーザーがサンプル YAML で S1 を完走できるよう、**テンプレート挿入**またはドキュメントリンクを 1 クリック以内で提供する（実装フェーズ P3.0 以降でよい）。

### Performance

- SSE 利用時も **GET のスパム**を避けるため、`GraphUpdated` 受信から GET まで **デバウンス**（例: 300ms〜1s）を design で選定する。

## Out of Scope

- `states` 形式のフル編集サポート、マルチテナント管理 UI、本番 RBAC。
- Core-API に **新規 validate エンドポイント**を必須としない（オプションは `ui-playground-design.md` §7）。

## References

- `.workspace-docs/30_specs/10_in-progress/ui-playground-design.md`
- `.workspace-docs/30_specs/10_in-progress/v2-ui-spec.md`
- `.workspace-docs/40_plans/10_in-progress/v2-roadmap.md`
- `docs/core-api-interface.md`
- `docs/statevia-data-integration-contract.md`
- `docs/ui-push-api-spec.md`
- `docs/ui-api-auth-tenant-config.md`
- `.workspace-docs/30_specs/30_archived/v2-playground-min-ui-spec.md`（歴史）
