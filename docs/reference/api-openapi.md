# OpenAPI と Scalar

| 項目 | 値 |
| --- | --- |
| 種別 | Reference |
| Version | 1.0 |
| 更新日 | 2026-07-07 |
| 関連 | [../specifications/api-http.md](../specifications/api-http.md) |

---

Core-API の HTTP 契約は **OpenAPI** と手書き Markdown の両方で提供されます。運用叙述・エラー方針・SSE の詳細は [api-http 仕様](../specifications/api-http.md) を正とし、本ページは閲覧・export の導線のみを示します。

## ローカルで閲覧

Core-API 起動後（Development または `STATEVIA_ENABLE_API_DOCS=true`）:

| リソース | URL |
| --- | --- |
| Scalar UI | `http://localhost:8080/scalar/v1` |
| OpenAPI JSON | `http://localhost:8080/swagger/v1/swagger.json` |

ポートは `ASPNETCORE_URLS` や launch profile に依存します。Compose では `8080` が一般的です。

## リポジトリに export

コミット用の OpenAPI JSON:

```powershell
.\scripts\export-core-api-openapi.ps1
```

出力: `service/api/openapi/core-api-v1.openapi.json`

## 本番での公開

Production イメージでは API ドキュメントは**既定オフ**です。有効化する場合は `STATEVIA_ENABLE_API_DOCS=true`（API 構造の露出に注意）。

## 次に読むもの

- HTTP 契約（叙述含む）: [../specifications/api-http.md](../specifications/api-http.md)
- リクエスト例: [../guides/http-request-examples.md](../guides/http-request-examples.md)
