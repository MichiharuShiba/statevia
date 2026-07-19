# 環境変数・設定キー（抜粋）

| 項目 | 値 |
| --- | --- |
| 種別 | Reference |
| Version | 1.4 |
| 更新日 | 2026-07-20 |

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

| キー | 説明 | 不正時 |
| --- | --- | --- |
| `Statevia:ActionHost:BaseUrl` | OutOfProcess 用 Action Host（絶対 http(s) URI） | 非空で不正 URI → **起動失敗**。未設定 → 実行時 `ActionHostNotConfigured` |
| `Statevia:Modules:Signing:*` | 署名検証・TrustLevel | （本表では検証対象外） |
| `Statevia:Modules:Oci:*` | OCI Module Source | 未設定時はソース無効 |
| `Statevia:Modules:S3:*` | S3 Module Source（`Enabled` / `Artifacts` 等） | 未設定時はソース無効 |
| `Statevia:Modules:Git:*` | Git Module Source（`Enabled` / `Artifacts` 等。HTTP archive・GitHub / GitLab） | 未設定時はソース無効 |
| `Statevia:ExecutionPolicy:*` | 実行モード下限・テナント Policy | 下表 Docker / Sandbox 制約を参照 |
| `Statevia:ExecutionPolicy:Sandbox:ContainerProvider` | Container Mode のランタイム（例: `docker`） | `Image` 未設定でも**起動は継続**（実行時 fail-safe） |
| `Statevia:ExecutionPolicy:Sandbox:TimeoutSeconds` | 実行タイムアウト秒（設定時 10〜3600） | 範囲外 → **起動失敗** |
| `Statevia:ExecutionPolicy:Sandbox:MemoryLimitMiB` | メモリ上限 MiB（設定時 64〜8192） | 範囲外 → **起動失敗** |
| `Statevia:ExecutionPolicy:Sandbox:CpuLimit` | CPU 上限（コア数換算、設定時 0.25〜8.0） | 範囲外 → **起動失敗** |
| `Statevia:ExecutionPolicy:Sandbox:Docker:Image` | 起動する Action Host 相当イメージ | 未設定 → 実行時 `SandboxRuntimeUnavailable`（起動は成功） |
| `Statevia:ExecutionPolicy:Sandbox:Docker:DefaultTimeoutSeconds` | 既定タイムアウト秒（10〜3600） | 範囲外 → **起動失敗** |
| `Statevia:ExecutionPolicy:Sandbox:Docker:GrpcPort` | コンテナ内 gRPC ポート（1024〜65535） | 範囲外 → **起動失敗** |
| `Statevia:ExecutionPolicy:Sandbox:Docker:NetworkMode` | Docker NetworkMode（`none` 不可。空白は `bridge`＋Warning） | `none` → **起動失敗** |
| `Auth:Jwt:SigningKey` | JWT 署名シークレット | 空 → **起動失敗** |
| `Auth:Jwt:AccessTokenLifetimeMinutes` | トークン有効期間（分、≥1） | 範囲外 → **起動失敗** |
| `ExecutionProjectionQueue:*` | Projection キュー（サイズ・リトライ遅延等） | 範囲外・矛盾 → **起動失敗** |
| `EventDelivery:Retry:*` | イベント配送リトライ | 範囲外・`MaxDelayMs < BaseDelayMs` → **起動失敗** |

検証方針（起動失敗 / 警告＋既定値 / 機能無効）の規約は [`docs/development-guidelines.md`](../development-guidelines.md) §4.1（Options 検証）を参照。

詳細: [AGENTS.md](../../AGENTS.md)、[actions/platform.md](../specifications/actions/platform.md)、[operations-docker.md](../guides/operations-docker.md)。

OpenAPI: [api-openapi.md](api-openapi.md)。
