# UI / API 認証・テナント設定

REST および Push（SSE）で Core API に認証・テナントヘッダを送るための設定方法。

---

## 1. 概要

- **REST**: UI の `api.ts` が `/api/core/*` にリクエストする際、認証・テナントヘッダを付与する。
- **中継**: Next.js の `route.ts`（`/api/core/[...path]`）が Core API に転送する際、リクエストのヘッダまたは環境変数から `Authorization` と `X-Tenant-Id` を付与する。
- **Push（SSE）**: `EventSource` はヘッダを送れないため、テナントはクエリ `?tenantId=...` で渡し、route が `X-Tenant-Id` に変換して転送する。

---

## 2. 環境変数

### 2.1 UI サーバー（Next.js）で設定するもの

| 変数名 | 必須 | 説明 |
|--------|------|------|
| `CORE_API_INTERNAL_BASE` | はい | Core API の内部 URL（例: `http://core-api:8080`）。既存仕様。 |
| `CORE_API_AUTH_TOKEN` | いいえ | Core API 用 Bearer トークン。`Bearer ` なしで設定すると自動で付与する。クライアントからヘッダが来ない場合に使用。 |
| `CORE_API_TENANT_ID` | いいえ | テナント ID。クライアントからヘッダ・クエリが来ない場合に使用。 |

### 2.2 クライアント（ブラウザ）用（ビルド時に埋め込まれる）

| 変数名 | 必須 | 説明 |
|--------|------|------|
| `NEXT_PUBLIC_TENANT_ID` | いいえ | クライアントから送るテナント ID。REST では `X-Tenant-Id`、SSE では `?tenantId=` に使う。 |
| `NEXT_PUBLIC_AUTH_TOKEN` | いいえ | クライアントから送る Bearer トークン。**注意**: ブラウザに露出するため、本番ではサーバー側の `CORE_API_AUTH_TOKEN` 利用を推奨。 |

---

## 3. ヘッダの優先順位（中継時）

route が Core API に転送するときの付与順序:

1. **Authorization**: リクエストの `Authorization` → なければ `CORE_API_AUTH_TOKEN`（`Bearer ` を付与）。
2. **X-Tenant-Id**: リクエストの `X-Tenant-Id` → なければ GET のストリーム時はクエリ `tenantId` → なければ `CORE_API_TENANT_ID`。

---

## 4. 設定パターン

### 認証不要・テナント不要（開発）

- 上記の認証・テナント用変数はすべて未設定でよい。
- テナント未指定時は UI に「テナントが未指定です」のバナーが表示される（設定すれば非表示）。

### 認証必須・テナント必須（本番想定）

- **サーバー側のみ**: `CORE_API_AUTH_TOKEN` と `CORE_API_TENANT_ID` を設定。クライアントにはトークンを渡さない。
- **クライアントでテナントを渡す**: `NEXT_PUBLIC_TENANT_ID` を設定。認証はサーバーで `CORE_API_AUTH_TOKEN` を設定。

### Docker Compose の例

```yaml
ui:
  environment:
    CORE_API_INTERNAL_BASE: "http://core-api:8080"
    CORE_API_AUTH_TOKEN: "your-service-token"
    CORE_API_TENANT_ID: "tenant-default"
```

---

## 5. テナント未指定時のエラー表示

- UI で `NEXT_PUBLIC_TENANT_ID` もサーバーで `CORE_API_TENANT_ID` も使わない場合、画面上に「テナントが未指定です。…」のバナーを表示する。
- API が 401 / 403 を返した場合は、トーストで「認証が必要」「権限不足またはテナント未指定」などのメッセージを表示する。

---

## 6. 関連仕様

- [UI Push API Specification](./ui-push-api-spec.md) — 認証・テナントヘッダの仕様
- [Core API Contract](./core-api-contract.md) — 共通ヘッダ（Authorization 任意）
