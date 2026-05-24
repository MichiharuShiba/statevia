# Runtime Security Boundary

- Version: 1.0.0
- 更新日: 2026-05-21
- 関連: `.spec-workflow/specs/runtime-security-boundary/`

---

## 概要

Core-API のテナント・Principal・認証・認可の境界を定義する。初版（フェーズ C 基盤）では **単一テナント前提の QueryFilter**、**JWT + 移行期 `X-Tenant-Id`**、**テナントライフサイクルの fail-closed** を固定する。

## テナント識別子

| 識別子 | 用途 | 変更 |
| --- | --- | --- |
| `tenants.tenant_id`（UUID） | 内部 FK・JWT クレーム | 不変 |
| `tenant_key` | SDK / CLI / `X-Tenant-Id` / 既存 `tenant_id` varchar | **immutable**（alias 施策まで変更しない） |

既存テーブルの `tenant_id` varchar は移行期 **`tenant_key` と同値**で運用する。UUID 参照への寄せは別マイグレーションとする。

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

### 移行期 `X-Tenant-Id`

- JWT **なし**: `X-Tenant-Id`（省略時 `"default"`）でテナントを解決する（従来互換）。
- JWT **あり** かつ `X-Tenant-Id` **あり**: **`tenant_key` が一致しない場合は 403**（`TENANT_HEADER_MISMATCH`）。
- `X-Tenant-Id` は **移行専用**であり恒久仕様ではない（`docs/core-api-interface.md` 参照）。

### Credential

| 種別 | 初版 |
| --- | --- |
| Password + JWT | 実装（`POST /v1/auth/login`） |
| API キー | スキーマ + ハッシュ保存。検証は後続タスク |
| OIDC / PAT | 設計余地のみ（未実装） |

パスワードは ASP.NET Core `PasswordHasher` で保存する。API キーは **平文非保存**（prefix + SHA-256 ハッシュ）。

## ログのテナント識別子

- HTTP ログ（`RequestLoggingMiddleware`）は引き続き **`TenantId` = `tenant_key`** を出力する（既存 `logging-property-naming.md` との整合）。
- 内部 UUID はデバッグ用途に限りトレースログへ載せる場合がある（本番ログへの一括出力は避ける）。
