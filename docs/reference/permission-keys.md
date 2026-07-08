# Permission Keys 一覧

| 項目 | 値 |
| --- | --- |
| 種別 | Reference |
| Version | 1.0 |
| 更新日 | 2026-07-08 |

---

Runtime API の **semantic permission key** の調べ物。Normative 契約は [api-http.md](../specifications/api-http.md) §4.1.2.1、[security-runtime.md](../specifications/platform/security-runtime.md) を正とする。

正本実装: `WellKnownPermissionKeys` / `PermissionCatalog`（`core/application/Statevia.Core.Application.Contracts/Security/`）。

## カタログ（初版）

| Permission key | 表示ラベル（参考） | i18n キー（参考） |
| --- | --- | --- |
| `definitions.read` | Read definitions | `permissions.definitionsRead` |
| `definitions.write` | Write definitions | `permissions.definitionsWrite` |
| `executions.read` | Read executions | `permissions.executionsRead` |
| `executions.write` | Write executions | `permissions.executionsWrite` |
| `tenant.admin` | Tenant administration | `permissions.tenantAdmin` |

- DB の `permissions` テーブルへ `EnsurePermissionCatalogAsync` で seed される。
- **`tenant.admin`**: テナント管理者（`/v1/admin/*`）。JWT で `is_tenant_admin` の Principal は **全 catalog key** を Live 展開で持つ。

## Runtime API との対応

| 操作（概要） | 必要な permission key |
| --- | --- |
| GET `/v1/definitions*`、`/v1/graphs/*`、`/v1/definitions/schema/nodes`、`/v1/actions/schema*` | `definitions.read` |
| POST / PUT `/v1/definitions` | `definitions.write` |
| GET `/v1/executions*`（一覧・詳細・graph・state・events・stream） | `executions.read` |
| POST start / cancel / publish event / resume | `executions.write` |

不足時: **403**、`error.code = PERMISSION_DENIED`（[error-codes.md](error-codes.md)）。

## 評価ルール（抜粋）

| 認証方式 | 有効 permission の決まり方 |
| --- | --- |
| JWT（ユーザー） | 所属グループの permission を Live 展開（`ExpandPrincipalPermissionKeysAsync`） |
| API キー | **`effective = 展開許可 ∩ allowed_scopes`**（交差のみ。deny リストなし） |

### プロジェクト認可（併用）

定義取得・publish・Start は **global permission** に加え `project_accesses` を評価する。

| 状況 | HTTP | `error.code` |
| --- | --- | --- |
| プロジェクト未登録・Reader 未満 | 404 | `NOT_FOUND`（存在秘匿） |
| Reader のみで Start | 403 | `PROJECT_ACCESS_DENIED` |

### Execution Security Snapshot（Resume / Cancel）

- **Owner**（Start 発行者）: Snapshot 上の `effectivePermissionKeys` で評価可（権限剥奪後も — Principal が有効なら）。
- **Operator**: 常に Live の `executions.write` を要求。

詳細: [security-runtime.md](../specifications/platform/security-runtime.md) Execution Security Snapshot、[execution-security-snapshot.md](../specifications/platform/execution-security-snapshot.md)。

## 関連

- HTTP 契約: [api-http.md](../specifications/api-http.md) §4.1.2.1
- セキュリティ境界: [security-runtime.md](../specifications/platform/security-runtime.md)
- エラーコード: [error-codes.md](error-codes.md)
