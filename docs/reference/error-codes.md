# HTTP エラーコード一覧

| 項目 | 値 |
| --- | --- |
| 種別 | Reference |
| Version | 1.0 |
| 更新日 | 2026-07-08 |

---

Core-API が返す `error.code` の**調べ物**一覧。Normative 契約は [api-http.md](../specifications/api-http.md) §4.3、[data-integration.md](../specifications/data-integration.md) §7 を正とする。

実装の写像は主に `ApiExceptionFilter` / `ApiErrorResult` / `TenantContextMiddleware` / 各 Service。

## 応答形（共通）

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "…",
    "details": { }
  }
}
```

- `details` は 422 の定義検証・ModelState 等で `{ "field", "message" }[]` またはオブジェクト。
- 機微値（action input/output 等）は IO-14 に従い `details` / ログに含めない。

## 一覧（HTTP ステータス順）

### 401 Unauthorized

| `error.code` | 典型状況 | 備考 |
| --- | --- | --- |
| `UNAUTHORIZED` | JWT / API キー不正、Principal 未解決、Runtime API で認証なし | 既定の認証失敗 |
| `TENANT_UNRESOLVED` | `X-Tenant-Id` の `tenant_key` が存在しない | [security-runtime.md](../specifications/platform/security-runtime.md) Lifecycle |

### 403 Forbidden

| `error.code` | 典型状況 | 備考 |
| --- | --- | --- |
| `PERMISSION_DENIED` | semantic permission key 不足 | [permission-keys.md](permission-keys.md) |
| `PROJECT_ACCESS_DENIED` | プロジェクト `project_accesses` のロール不足（例: Reader のみで Start） | 存在秘匿のため未登録は **404** |
| `FORBIDDEN` | テナント管理者必須、テナント非 Active（汎用）、その他拒否 | Admin API 等 |
| `TENANT_SUSPENDED` | テナント `Suspended` | fail-closed |
| `TENANT_ARCHIVED` | テナント `Archived` | 復帰不可 |
| `TENANT_HEADER_MISMATCH` | JWT `tenant_key` と `X-Tenant-Id` 不一致 | 移行期 |
| `PRINCIPAL_INACTIVE` | Principal 無効（`disabled_at` / `deleted_at` / `is_active=false`） | Resume / Cancel 等 |

### 404 Not Found

| `error.code` | 典型状況 | 備考 |
| --- | --- | --- |
| `NOT_FOUND` | リソース未存在、他テナント秘匿 | 定義・実行・graph 等 |

### 409 Conflict

| `error.code` | 典型状況 | 備考 |
| --- | --- | --- |
| `IDEMPOTENCY_KEY_CONFLICT` | 同一 `X-Idempotency-Key` で本文が異なる | `POST /v1/executions` のみ |

状態競合（例: cancel 後 resume）の 409 と `COMMAND_REJECTED` 例は [data-integration.md](../specifications/data-integration.md) §7（実装コードは未固定の場合あり）。

### 422 Unprocessable Entity

| `error.code` | 典型状況 | 備考 |
| --- | --- | --- |
| `VALIDATION_ERROR` | 入力検証失敗、定義コンパイル失敗、`ArgumentException` | 既定。`error.details` あり得る |
| `DEFINITION_MIGRATION_REQUIRED` | 版共存・Legacy 定義の再 compile が必要 | Module 複数版ロード等 |
| `MODULE_VERSION_RESOLUTION_FAILED` | compile 時の Module 版解決失敗 | alias / range 不一致 |

### 500 Internal Server Error

| `error.code` | 典型状況 | 備考 |
| --- | --- | --- |
| `INTERNAL_ERROR` | 未処理例外 | クライアント向けに内部詳細を含めない |

## 関連

- HTTP 契約: [api-http.md](../specifications/api-http.md)
- UI エラー表示: [data-integration.md](../specifications/data-integration.md) §7
- OpenAPI: [api-openapi.md](api-openapi.md)
