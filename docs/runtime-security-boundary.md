# Runtime Security Boundary

- Version: 1.2.1
- 更新日: 2026-06-08
- 関連: `.spec-workflow/specs/runtime-security-boundary/`

---

## 概要

Core-API のテナント・Principal・認証・認可の境界を定義する。現行（E3 反映）では **単一テナント前提の QueryFilter**、**JWT / API キー + 移行期 `X-Tenant-Id`**、**Runtime API の Principal 必須化**、**テナントライフサイクルの fail-closed** を固定する。

## テナント識別子

| 識別子 | 用途 | 変更 |
| --- | --- | --- |
| `tenants.tenant_id`（UUID） | 内部 FK・JWT クレーム | 不変 |
| `tenant_key` | SDK / CLI / `X-Tenant-Id` / 既存 `tenant_id` varchar | **immutable**（alias 施策まで変更しない） |

実行系テーブルの `tenant_id` は **`tenants.tenant_id`（UUID）FK**（マイグレーション `ExecutionTenantIdUuidFk`）。HTTP の `X-Tenant-Id` は引き続き **`tenant_key`**。

## LifecycleTransitionPolicy

### 状態

| 値 | 意味 |
| --- | --- |
| `Active` | 通常利用可能 |
| `Suspended` | 一時停止（支払停止・abuse 等）。API は fail-closed |
| `Archived` | 論理終了。復帰不可 |

### 初期状態

- 新規テナント作成時の初期状態は **`Active`**。

### 許可遷移

| From | To |
| --- | --- |
| `Active` | `Suspended`, `Archived` |
| `Suspended` | `Active`, `Archived` |
| `Archived` | （なし） |

`Archived` から `Active` / `Suspended` への復帰は **禁止**。

### API 挙動（非 Active）

| 状態 | HTTP | コード |
| --- | --- | --- |
| `Suspended` | 403 Forbidden | `TENANT_SUSPENDED` |
| `Archived` | 403 Forbidden | `TENANT_ARCHIVED` |
| テナント未解決 | 401 Unauthorized | `TENANT_UNRESOLVED` |

## CrossTenantExecutionPolicy

**現時点: 未確定（単一テナント前提）**

- Tenant A から Tenant B のワークフローを起動する federation は **採用しない**。
- QueryFilter は **現在解決済みテナント 1 件**のみを対象とする。
- 将来 federation を許可する場合は、本節を改訂し、専用 capability と監査経路を追加してから実装する。

## ITenantContext と HasQueryFilter

- **`ITenantContextAccessor`**（AsyncLocal ベース）が HTTP / ワーカー共通のテナント文脈を保持する。
- **`ITenantContext` 未設定（fail-closed）** 時、テナント列を持つ DbSet の `HasQueryFilter` は **0 件**を返す。
- **`IgnoreQueryFilters()`** は **`IPlatformDataAccess`**（platform 専用層）にのみ許容する。それ以外での利用はレビュー必須。

## TenantExecutionScope

- バックグラウンド処理（投影キュー等）では **`TenantExecutionScope`** を標準入口とする。
- AsyncLocal **単独依存を避け**、重要パスでは明示的な tenant 引数との併用余地を残す（設計上の推奨）。

## 認証（初版）

### JWT クレーム

| クレーム | 内容 |
| --- | --- |
| `tenant_id` | テナント内部 UUID |
| `tenant_key` | 外部向けキー |
| `principal_id` | 実行主体 UUID |
| `sub` | principal_id と同値（互換） |

### 移行期 `X-Tenant-Id` と Runtime API 認証

- Runtime API（`/v1/definitions` / `/v1/executions`）は **Principal 必須**。JWT または API キーで `ITenantContext.PrincipalId` を解決する。
- JWT / API キー **なし** + `X-Tenant-Id` のみ: Runtime API は **401**（`UNAUTHORIZED`）。
- JWT **あり** かつ `X-Tenant-Id` **あり**: **`tenant_key` が一致しない場合は 403**（`TENANT_HEADER_MISMATCH`）。
- `X-Tenant-Id` は **移行専用**であり恒久仕様ではない（`docs/core-api-interface.md` 参照）。

### Credential

| 種別 | 初版 |
| --- | --- |
| Password + JWT | 実装（`POST /v1/auth/login`） |
| API キー | 実装（`X-Api-Key`、prefix + SHA-256 hash 検証、`last_used_at` 更新） |
| OIDC / PAT | 設計余地のみ（未実装） |

パスワードは ASP.NET Core `PasswordHasher` で保存する。API キーは **平文非保存**（prefix + SHA-256 ハッシュ）。有効スコープは **`effective = expanded_permissions ∩ allowed_scopes`**（deny リストなし）とする。

## ログのテナント識別子

- HTTP ログ（`RequestLoggingMiddleware`）は解決済み **`TenantId={tenantId}`**（`tenants.tenant_id` UUID）を出力する。テナント解決をスキップするパスでは値は null。
- 実行系・dedup 系の構造化ログも同様に内部 UUID を `TenantId` キーで載せる。

## Execution Security Snapshot（E4）

Start 成功時に `ExecutionSecuritySnapshot` を `executions.security_snapshot_json`（**`text`** — 認可用 BLOB）に保存する。分析・監査の検索・集計は **別テーブルへ投影**する想定（本列を `jsonb` 化したり SQL で部分参照しない）。詳細は [`.spec-workflow/specs/runtime-security-boundary/execution-security-snapshot.md`](../.spec-workflow/specs/runtime-security-boundary/execution-security-snapshot.md)。

| 操作 | Identity | Authorization |
| --- | --- | --- |
| Start | Live | Live（`executions.write`）。成功時 Snapshot 作成 |
| Resume / Cancel | Live | **Owner**: `evaluationMode`（既定 **Snapshot**） / **Operator**: 常に Live |
| Read | Live | Live（`executions.read`） |

- **Owner** = `startedByPrincipalId`（Start 発行者）。Snapshot 上の `effectivePermissionKeys` で Owner 経路を評価できる（権限剥奪後も Resume / Cancel 可 — Principal が有効な場合）。
- **Operator** = Owner 以外。常に Live の `executions.write` を要求。
- スナップショット未保存の execution（移行前データ）は Resume / Cancel で **Live** にフォールバック。
- Principal 無効化（`disabled_at` / `deleted_at` / `is_active=false`）は Identity で **403**（`PRINCIPAL_INACTIVE`）。
