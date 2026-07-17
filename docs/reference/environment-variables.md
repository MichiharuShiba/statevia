# 環境変数・設定キー（抜粋）

| 項目 | 値 |
| --- | --- |
| 種別 | Reference |
| Version | 1.0 |
| 更新日 | 2026-07-07 |

---

Core-API / UI / Module の主要な環境変数と `appsettings` キー。**調べ物**用。Normative 契約は各 Specification を正とする。

## インフラ

| 名前 | サービス | 説明 |
| --- | --- | --- |
| `DATABASE_URL` | core-api | PostgreSQL 接続文字列 |
| `ASPNETCORE_URLS` | core-api | バインド URL（例: `http://0.0.0.0:8080`） |
| `CORE_API_INTERNAL_BASE` | ui | UI → Core-API プロキシ先 |

## 運用・デバッグ

| 名前 | 説明 |
| --- | --- |
| `STATEVIA_ENABLE_API_DOCS` | 本番で OpenAPI / Scalar を有効化 |
| `STATEVIA_LOG_HTTP_BODIES` | HTTP 本文ログ（機密に注意） |
| `STATEVIA_MODULES_PATH` | Action Module ルート（未設定時は設定ファイル） |

## Action / 実行ポリシー（appsettings）

| キー | 説明 |
| --- | --- |
| `Statevia:ActionHost:BaseUrl` | OutOfProcess 用 Action Host |
| `Statevia:Modules:Signing:*` | 署名検証・TrustLevel |
| `Statevia:Modules:Oci:*` | OCI Module Source |
| `Statevia:Modules:S3:*` | S3 Module Source（`Enabled` / `Artifacts` 等） |
| `Statevia:Modules:Git:*` | Git Module Source（`Enabled` / `Artifacts` 等。HTTP archive・GitHub / GitLab） |
| `Statevia:ExecutionPolicy:*` | 実行モード下限・テナント Policy |
| `Statevia:ExecutionPolicy:Sandbox:ContainerProvider` | Container Mode のランタイム（例: `docker`） |
| `Statevia:ExecutionPolicy:Sandbox:Docker:*` | Docker サンドボックス（`Image` / `ActionRuntimeProfile` / `ModulesHostPath` 等） |

詳細: [AGENTS.md](../../AGENTS.md)、[actions/platform.md](../specifications/actions/platform.md)、[operations-docker.md](../guides/operations-docker.md)。

OpenAPI: [api-openapi.md](api-openapi.md)。
