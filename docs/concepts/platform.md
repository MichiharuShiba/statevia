# プラットフォーム境界

| 項目 | 値 |
| --- | --- |
| 種別 | Concept |
| Version | 1.0 |
| 更新日 | 2026-07-07 |
| 関連 | [../specifications/platform/](../specifications/platform/) |

---

Statevia プラットフォームは **Core-API** を境界として、認証・テナント・永続化・Module 実行・UI 連携を統合します。Engine はドメインカーネルとしてプロセス内に載り、信頼できないコードは Policy に従いプロセス外・サンドボックスへ逃がします。

## レイヤーの責務

| 層 | 役割 |
| --- | --- |
| UI | 表示と Command 発行。`/api/core/*` で Core-API にプロキシ |
| Core-API | HTTP 契約、認可、UoW、Engine 起動、Module ホスト |
| Application | ユースケースの orchestration |
| Engine | 純粋な状態遷移ロジック |
| Infrastructure | EF Core、JWT、Module Source、Action 実行バックエンド |

テナント境界は **認証後も必須**です。`X-Tenant-Id` は外部キー（`tenant_key`）であり、リソースアクセスは常にテナントスコープで検証されます。

## セキュリティと観測

- **Runtime Security Boundary**: 信頼レベルと実行モードの関係
- **Execution Security Snapshot**: 実行時に記録するセキュリティ文脈
- **IO ログマスキング**: `input` / `output` 等の機密をログから守る（IO-14）
- **監査・再現性**: 将来の監査テーブルとイベントの関係

HTTP エラー形式・SSE・冪等キーは API 仕様に、永続フィールドの意味はデータ連携契約に記載します。

## 次に読むもの

| 内容 | ドキュメント |
| --- | --- |
| システム俯瞰 | [architecture/overview.md](../architecture/overview.md) |
| HTTP 契約 | [specifications/api-http.md](../specifications/api-http.md) |
| セキュリティ仕様 | [specifications/platform/security-runtime.md](../specifications/platform/security-runtime.md) |
| 運用（Docker） | [operations-docker.md](../guides/operations-docker.md) |
| 認証・テナント | [guides/ui-auth-tenant-config.md](../guides/ui-auth-tenant-config.md) |
