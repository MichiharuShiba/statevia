# ![statevia icon](docs/images/icon-mark-32.png) statevia

**YAML でワークフローを宣言し、実行・可視化・拡張まで一貫して扱えるプラットフォーム**です。

Definition（定義）を正本に、ビジネスプロセスを **実行可能なワークフロー** として動かします。  
初めての方は、まず [Quick Start](#quick-start) で動かしてみてください。

---

## Statevia でできること

| | |
| --- | --- |
| **Definition → 実行** | YAML/JSON の定義を publish し、その版に固定した実行を開始する |
| **Durable Execution** | 進行状態を永続化し、API / UI から再開・確認できる |
| **Event Driven** | 事実（完了・失敗・キャンセル等）に基づいて状態が遷移する |
| **Extensible Actions** | Action Module としてビジネスロジックを追加・配布できる |
| **Visual Workflow** | Studio UI で実行グラフを可視化し、進行を追える |

Fork / Join、Wait、協調的キャンセルなど、非同期・並列の業務処理向けの制御を定義から組み立てられます。

---

## Quick Start

初回安定版リリース前のため、取得タイミングやブランチによっては手順どおりに動かない場合があります。

**Clone → DB 起動 → マイグレーション → 全サービス起動 → 実行** まで試せます。詳細は [getting-started](docs/guides/getting-started.md) を参照してください。

**前提:** Docker / Docker Compose。初回の DB 作成には **.NET 8 SDK**（ホストから `dotnet ef`）。以下の curl 例には **`jq`**。

```bash
git clone https://github.com/MichiharuShiba/statevia.git
cd statevia
cp .env.example .env          # 初回のみ

docker compose up -d postgres
# 初回のみ: 空の PostgreSQL にスキーマを作成（ホストから接続するため DB ホストは localhost）
set -a && source .env && set +a
export DATABASE_URL="postgres://${POSTGRES_USER}:${POSTGRES_PASSWORD}@localhost:${POSTGRES_PORT}/${POSTGRES_DB}"
(cd service/api && dotnet ef database update --project Statevia.Service.Api)

docker compose up -d
```

既に `docker compose up -d` 済みで API が落ちている場合は、マイグレーション後に `docker compose restart service-api`。

| 確認 | URL / コマンド |
| --- | --- |
| API ヘルス | `curl -s http://localhost:8080/v1/health` |
| API ドキュメント | [http://localhost:8080/scalar/v1](http://localhost:8080/scalar/v1) |
| Studio UI | [http://localhost:3000](http://localhost:3000) |

**開発用の既定アカウント**（`ASPNETCORE_ENVIRONMENT=Development` で API 起動時に自動作成。初回マイグレーション後）:

| 項目 | 値 |
| --- | --- |
| テナント | `default` |
| ユーザー（email） | `admin` |
| パスワード | `admin` |

**1 回実行する**（ログイン → 定義登録 → 実行）:

```bash
# トークン取得
TOKEN=$(curl -s -X POST http://localhost:8080/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"tenantKey":"default","email":"admin","password":"admin"}' | jq -r .accessToken)

# 定義を初回登録（POST。PUT は既存定義への版追加のみ）
DEF_ID=$(curl -s -X POST "http://localhost:8080/v1/definitions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d "$(jq -n --rawfile yaml docs/samples/ui-customer-order-parallel.yaml \
    '{name:"my-workflow",yaml:$yaml}')" | jq -r .displayId)

# 実行を開始（definitionId は登録応答の displayId）
curl -s -X POST "http://localhost:8080/v1/executions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Idempotency-Key: demo-run-1" \
  -d "{\"definitionId\":\"$DEF_ID\",\"input\":{}}"
```

UI からは [http://localhost:3000/login](http://localhost:3000/login) で同じ資格情報を使えます。

→ セットアップ・認証・トラブルシュート: **[docs/guides/getting-started.md](docs/guides/getting-started.md)**

Engine ライブラリだけ試す: [hello-statevia](core/engine/samples/hello-statevia) / [engine-standalone-guide](docs/guides/engine-standalone-guide.md)

---

## 推奨読書順

初めて Statevia を学ぶときは、次の順がおすすめです。

```text
Quick Start（この README）
    ↓
Guides — 手順・運用
    ↓
Concepts — なぜ・全体像
    ↓
Specifications — 契約・必須要件
    ↓
Reference — 辞書・一覧（必要になったら）
```

---

## Guides — やりたいことから

| やりたいこと | ドキュメント |
| --- | --- |
| **定義を書く** | [concepts/definition](docs/concepts/definition.md) → [specifications/definition](docs/specifications/definition.md) |
| **実行する** | [getting-started](docs/guides/getting-started.md) · [http-request-examples](docs/guides/http-request-examples.md) |
| **状態を確認する** | [ui-user-guide](docs/guides/ui-user-guide.md) |
| **拡張する** | [actions 概念](docs/concepts/actions.md) · [module-zip-layout](docs/specifications/actions/module-zip-layout.md) |

その他の手順一覧: [docs/guides/README.md](docs/guides/README.md)

---

## ドキュメント

詳細な索引は **[docs/README.md](docs/README.md)** にあります。カテゴリ別の入口:

| カテゴリ | 内容 | 入口 |
| --- | --- | --- |
| **Concepts** | 思想・全体像 | [docs/concepts/](docs/concepts/README.md) |
| **Specifications** | HTTP / 定義 / 実行の契約 | [docs/specifications/](docs/specifications/README.md) |
| **Reference** | スキーマ・ログキー・env 等 | [docs/reference/](docs/reference/README.md) |
| **Architecture** | レイヤー・リポジトリ構成 | [docs/architecture/](docs/architecture/README.md) |
| **Decisions** | 設計判断（ADR） | [docs/decisions/](docs/decisions/README.md) |
| **Future** | 未実装の構想 | [docs/future/](docs/future/README.md) |

---

## 実行イメージ

定義（Definition）→ 実行フロー → ExecutionGraph として観測されます。

![ExecutionGraph の例](docs/images/execution-graph-example.png)

サンプル定義: [docs/samples/ui-customer-order-parallel.yaml](docs/samples/ui-customer-order-parallel.yaml)  
実行モデルの概要: [docs/concepts/execution-model.md](docs/concepts/execution-model.md)

---

## リポジトリ構成

```text
statevia/
├─ core/              # Engine · Application
├─ infrastructure/    # 永続化 · Module · Security
├─ service/           # Core-API · CLI · action-host
├─ ui/studio/         # Web UI
└─ docs/              # ドキュメント正本
```

---

## 開発者・コントリビュータ向け

| 内容 | ドキュメント |
| --- | --- |
| ドキュメント体系・執筆ルール | [docs/README.md](docs/README.md) · [DOCUMENTATION-STANDARD](docs/DOCUMENTATION-STANDARD.md) |
| コーディング方針 | [development-guidelines](docs/development-guidelines.md) |
| 起動・テスト・環境変数 | [AGENTS.md](AGENTS.md) |

---

## License

MIT

---

## ステータス

本プロジェクトは設計・開発中です。初回安定版リリース前に破壊的変更が発生する可能性があります。
