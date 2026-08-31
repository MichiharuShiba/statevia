# UI / API 認証・テナント設定

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 1.1 |
| 更新日 | 2026-09-01 |
| 関連 | [api-http.md](../specifications/api-http.md), [environment-variables.md](../reference/environment-variables.md) |

---

Studio（Next.js）から Service API へ認証・テナントを渡す設定。Runtime API は JWT または `X-Api-Key` と `X-Tenant-Id` が必須です。

---

## 1. 概要

- **REST**: UI が `/api/core/*` にリクエストする際、セッション Cookie または（開発時のみ）環境変数から認証・テナントヘッダを付与する。
- **中継**: Next.js の `/api/core/[...path]` が Service API へ転送する。
- **Push（SSE）**: `EventSource` はヘッダを送れないため、テナントはクエリ `?tenantId=...` で渡し、route が `X-Tenant-Id` に変換して転送する。

---

## 2. 環境変数

### 2.1 UI サーバー（Next.js）で設定するもの

| 変数名 | 必須 | 説明 |
| --- | --- | --- |
| `SERVICE_API_INTERNAL_BASE` | はい | Service API の内部 URL（例: `http://service-api:8080`）。 |
| `SERVICE_API_AUTH_TOKEN` | いいえ | **開発用**バイパス。Cookie もリクエスト `Authorization` も無いときに Bearer を付ける。`NODE_ENV=production` では無視する。 |
| `SERVICE_API_TENANT_ID` | いいえ | テナント ID。クライアントからヘッダ・クエリが来ない場合に使用。 |

### 2.2 クライアント（ブラウザ）用（ビルド時に埋め込まれる）

| 変数名 | 必須 | 説明 |
| --- | --- | --- |
| `NEXT_PUBLIC_TENANT_ID` | いいえ | クライアントから送るテナント ID。REST では `X-Tenant-Id`、SSE では `?tenantId=` に使う。 |
| `NEXT_PUBLIC_AUTH_TOKEN` | いいえ | **開発用**。クライアントが `Authorization` に載せる。ブラウザに露出する。`NODE_ENV=production` では無視する。 |

---

## 3. ヘッダの優先順位（中継時）

route が Service API に転送するときの付与順序:

1. **Authorization**: セッション Cookie → リクエストの `Authorization` → （`NODE_ENV` が production でなければ）`SERVICE_API_AUTH_TOKEN`（`Bearer ` を付与）。
2. **X-Tenant-Id**: テナント Cookie → リクエストの `X-Tenant-Id` → GET のストリーム時はクエリ `tenantId` → `SERVICE_API_TENANT_ID`。

---

## 4. 設定パターン

### 開発（`NODE_ENV` が production 以外）

- ログインせずに試す場合: `SERVICE_API_AUTH_TOKEN`（必要なら `SERVICE_API_TENANT_ID`）をサーバー側だけに置く。
- クライアントにトークンを埋め込む場合は `NEXT_PUBLIC_AUTH_TOKEN`。ブラウザに露出するため、共有環境では使わない。

### 本番（`NODE_ENV=production`）

- `SERVICE_API_AUTH_TOKEN` と `NEXT_PUBLIC_AUTH_TOKEN` は無視する。Cookie 無しでは保護パスと `/api/core` を通さない。
- 利用者は Studio のログイン（JWT セッション Cookie）を使う。

### Docker Compose の例（開発）

```yaml
ui-studio:
  environment:
    SERVICE_API_INTERNAL_BASE: "http://service-api:8080"
    SERVICE_API_AUTH_TOKEN: "your-service-token"
    SERVICE_API_TENANT_ID: "tenant-default"
```

---

## 5. テナント未指定時のエラー表示

- UI で `NEXT_PUBLIC_TENANT_ID` もサーバーで `SERVICE_API_TENANT_ID` も使わない場合、画面上に「テナントが未指定です。…」のバナーを表示する。
- API が 401 / 403 を返した場合は、トーストで「認証が必要」「権限不足またはテナント未指定」などのメッセージを表示する。

---

## 6. 関連仕様

- [push-api 仕様](../specifications/ui/push-api.md) — SSE のテナント受け渡し
- [api-http 仕様](../specifications/api-http.md) — Runtime API の認証・`executions.write`
- [environment-variables.md](../reference/environment-variables.md) — JWT SigningKey と UI 開発トークン
