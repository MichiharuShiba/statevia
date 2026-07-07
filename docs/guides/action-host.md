# Action Host ガイド

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 1.0 |
| 更新日 | 2026-07-07 |
| 関連 | [../concepts/actions.md](../concepts/actions.md), [guides/operations-docker.md](guides/operations-docker.md) |

---

**Action Host**（`service/action-host/`）は、Policy が **OutOfProcess** を要求する Action を gRPC で実行するサンドボックスプロセスです。Core-API から `Statevia:ActionHost:BaseUrl` で到達します。

## いつ必要か

- Community / Verified 等の Module で本番ポリシーが OutOfProcess を返す場合
- プロセス内（InProcess）では隔離が不十分な運用プロファイル（例: `saas-shared`）

未設定時、OutOfProcess が必要な実行は `ActionHostNotConfigured` で失敗します（安全側）。

## ローカル起動（例）

PostgreSQL と Core-API と併せて起動する場合は Docker Compose が簡便です（[guides/operations-docker.md](guides/operations-docker.md)）。

単体で Action Host をホスト起動する例:

```bash
cd service/action-host
dotnet run --project Statevia.Service.ActionHost
```

既定 URL は launchSettings に依存します。Core-API の `appsettings` または環境変数で合わせます:

```json
"Statevia": {
  "ActionHost": {
    "BaseUrl": "http://localhost:5001"
  }
}
```

## Docker Compose

`docker compose` ではサービス名 `action-host`、Core-API から `http://action-host:5001` を参照する構成が一般的です。

## Module の reload

Module zip を配置したあと、Core-API が新 DLL を読み込むには再起動または `POST /internal/modules/reload` が必要です（[guides/operations-docker.md](guides/operations-docker.md)、[cli-reference.md](cli-reference.md)）。

## 次に読むもの

- Action プラットフォーム仕様: [../specifications/actions/platform.md](../specifications/actions/platform.md)
- Module zip: [../specifications/actions/module-zip-layout.md](../specifications/actions/module-zip-layout.md)
