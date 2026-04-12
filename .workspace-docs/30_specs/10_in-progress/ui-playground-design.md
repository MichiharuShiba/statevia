# UI Playground 設計（Phase 3）

- Version: 0.1.0
- 更新日: 2026-04-12
- 対象: `services/ui` における **Playground**（定義の編集・検証・登録と、実行の開始・操作・可視化を一体で行う開発者向け UI）
- 関連: `.workspace-docs/40_plans/10_in-progress/v2-roadmap.md`（Phase 3）、`v2-ui-spec.md`、`docs/core-api-interface.md`、`docs/statevia-data-integration-contract.md`、`docs/ui-push-api-spec.md`
- 歴史参照（アーカイブ）: `.workspace-docs/30_specs/30_archived/v2-playground-min-ui-spec.md`、`.workspace-docs/30_specs/30_archived/v2-statevia-playground-spec.md`

---

## 1. 目的と位置づけ

### 1.1 目的

- **Workflow を短いサイクルで試す**: YAML（nodes 形式）を編集し、Core-API に定義を登録し、すぐに実行して **ExecutionGraph・タイムライン・状態** を確認できること。
- **学習・デバッグ**: Cancel / イベント送信 / Wait の resume など、コマンドと Read Model の対応を UI 上で追えること。

### 1.2 非目標（MVP ではやらない）

- マルチユーザー協調編集、権限モデル、本番運用向けの定義バージョン管理 UI。
- Engine / API のプロセス分離や、Playground 専用の別バックエンド（**現状は既存 Core-API + 同一 DB** を前提）。
- `states` 形式ワークフローのフルサポート（nodes 優先。`v2-nodes-to-states-conversion-spec.md` の範囲外は明示的にスコープ外とする）。

### 1.3 既存 UI との関係

| 領域 | 現状（`services/ui/app/page.tsx` 付近） | Playground で追加するもの |
|------|----------------------------------------|----------------------------|
| 実行 ID 指定・Read | `useExecution` / グラフ・タイムライン・リプレイ | 同一フック・コンポーネントの **再利用**（レイアウト差し替え） |
| 定義・開始 | 手動（別途 API で定義登録が前提） | **YAML エディタ**、`POST /v1/definitions`、`POST /v1/workflows` を画面から実行 |
| リアルタイム | ポーリング中心（`useExecution`） | **任意で SSE**（`GET /v1/workflows/{id}/stream`）を併用可能にする設計（後述） |

**方針**: Playground は **新規ルート**（例: `/playground`）を推奨し、既存の「実行ダッシュボード」ルート（例: `/`）は **深いリンク**（「この実行をダッシュボードで開く」）で連携できるようにする。同一ページに全部詰めるとワイヤーが破綻しやすいため、**MVP は 2 カラム＋ドロワー**程度に抑える。

---

## 2. ユーザーと主要シナリオ

| シナリオ | 成功条件（MVP） |
|----------|----------------|
| S1: 新規定義から初実行 | YAML → 保存（`POST /v1/definitions`）→ Start（`POST /v1/workflows`）→ `displayId` で Read・グラフが表示される |
| S2: 既存定義の再実行 | 定義一覧または `definitionId` 入力から Start のみ |
| S3: 実行中操作 | Cancel / イベント名送信 / Wait の resume が `core-api-interface.md` どおり動き、409/422 がトーストで分かる |
| S4: 状態の追従 | ポーリングまたは SSE でグラフが更新され、タイムラインと整合する（Read-model authority に従い **GET が正**） |

---

## 3. 情報アーキテクチャ（ルートと画面）

### 3.1 推奨ルート（Next.js App Router）

| パス | 内容 |
|------|------|
| `/playground` | Playground ホーム（エディタ + 簡易 Runner + 結果ペイン） |
| `/playground/run/[displayId]` | 実行フォーカス（既存 `page.tsx` の構成に近い **Runner ビュー**。クエリで `?from=playground` 等も可） |

**理由**: SEO 不要な開発 UI はパス分離し、バンドル分割と認証ゲート（将来）を入れやすくする。

### 3.2 MVP ワイヤー（1 ページ `/playground`）

```text
+----------------------------------------------------------------------------------+
| AppBar: Statevia Playground    [Tenant]   [Docs リンク]                          |
+----------------------------------+-----------------------------------------------+
| Definition                       | Run & observe                                |
| - YAML (Monaco または textarea)  | - definitionId / input(JSON)                 |
| - [Register definition]          | - [Start]  → 表示 displayId / resourceId     |
| - サーバーエラー（400）表示      | - [Cancel] [Send event] [Resume node]        |
|                                  | - Status バッジ + 最終 GET 時刻               |
+----------------------------------+-----------------------------------------------+
| (任意) バリデーション文言一覧    | Graph（React Flow）| Timeline | Node detail |
|                                  | （既存コンポーネント流用）                     |
+----------------------------------+-----------------------------------------------+
```

- **Register**: `POST /v1/definitions`（現行 API に **validate 専用エンドポイントはない**。不正 YAML は **400** とレスポンス本文で検知。将来 `POST /v1/definitions/validate` を追加する場合は本書を改訂）。
- **Start**: `POST /v1/workflows`（`definitionId` は直前に登録した `displayId` を自動入力できるとよい）。

---

## 4. API 契約マッピング（Playground → Core-API）

| UI アクション | HTTP（プロキシ後の UI パスは `docs/core-api-interface.md` §5 準拠） | 備考 |
|---------------|---------------------------------------------------------------------|------|
| 定義登録 | `POST /v1/definitions` | body: `name`, `yaml` |
| ワークフロー開始 | `POST /v1/workflows` | `X-Idempotency-Key` 推奨 |
| Read | `GET /v1/workflows/{id}` | displayId / UUID 両対応 |
| グラフ JSON | `GET /v1/workflows/{id}/graph` | スナップショット |
| Graph 定義（静的） | `GET /v1/graphs/{graphId}` | `useGraphDefinition` と同じ契約 |
| キャンセル | `POST /v1/workflows/{id}/cancel` | 204 |
| イベント | `POST /v1/workflows/{id}/events` | 204 |
| Resume | `POST /v1/workflows/{id}/nodes/{nodeId}/resume` | 204 |
| タイムライン | `GET /v1/workflows/{id}/events` | `afterSeq`, `limit` |
| リプレイ近似 | `GET /v1/workflows/{id}/state?atSeq=` | 既存 UI と同様 |
| SSE（任意） | `GET /v1/workflows/{id}/stream` | `EventSource` + テナントクエリは `docs/ui-api-auth-tenant-config.md` |

---

## 5. 状態管理とデータフロー

### 5.1 クライアント状態（推奨）

- **definitionYaml**, **definitionName**, **lastRegisteredDisplayId**（登録成功時）
- **workflowInput**（JSON 文字列またはオブジェクト）
- **activeDisplayId**（現在観測中の実行）
- **sseEnabled**（boolean。既定 false でポーリングのみでも可）

### 5.2 同期戦略

1. **Read の正**: `GET /v1/workflows/{id}` / `…/graph`（`statevia-data-integration-contract.md` / `AGENTS.md`）。
2. **SSE**: `GraphUpdated` 受信後に **必ず GET で確定**（過剰フェッチを避けるため、SSE 受信をトリガにしたデバウンス GET でもよい）。
3. **ポーリング**: 既存 `useExecution` の間隔を Playground 用に短くするオプション（例: 実行中のみ 1s）を検討。

---

## 6. 実装フェーズ（提案）

| 段階 | 内容 | 完了の目安 |
|------|------|------------|
| **P3.0** | `/playground` ルート、YAML + Register + Start + `activeDisplayId` 表示 | S1 が手動で通る |
| **P3.1** | 既存 Graph / Timeline / NodeDetail を埋め込み、`/playground/run/[displayId]` への遷移 | S3 が通る |
| **P3.2** | SSE トグル、`GET /v1/graphs` 連携のエラー表示統一、E2E（任意） | S4 |

---

## 7. オープン検討事項

1. **Monaco** vs 軽量 `textarea`（MVP は textarea でも可。Monaco は bundle サイズと設定コスト）。
2. **定義 validate API** を Core-API に追加するか（現状は不要でも、エディタ UX 向上には有効）。
3. **サンプル YAML** の同梱（`engine/samples` 連動 or UI 内テンプレート）。
4. **認証**: 現状なし。Phase 4 / SaaS 前に Playground を **ローカル専用**と明示するか、簡易トークンを挟むか。

---

## 8. 参照索引

| ドキュメント | 用途 |
|--------------|------|
| `v2-ui-spec.md` | Definition Editor / Runner の機能粒度・表示ルール |
| `v2-roadmap.md` §Phase 3 | プロダクト上の位置 |
| `docs/core-api-interface.md` | HTTP 正本 |
| アーカイブ `v2-playground-min-ui-spec.md` | ワイヤー・コンポーネント名の歴史案 |

---

## 変更履歴

| 版 | 日付 | 内容 |
|----|------|------|
| 0.1.0 | 2026-04-12 | 初版。現行 UI / Core-API / 契約ドキュメントに整合した MVP 範囲とフェーズ分割。 |
