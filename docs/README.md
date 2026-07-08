# Statevia ドキュメント

**Statevia** は **Definition Driven Execution Platform** です。定義（Definition）を起点に実行・拡張・運用を行います。

---

## Documentation Principles

1. ドキュメントは利用者の理解を助けるために存在する。
2. 必要になるまで新しいカテゴリやディレクトリは増やさない。
3. ファイル数より理解しやすさを優先する。
4. 実装構造ではなく概念構造を優先する。
5. 知識体系は将来進化してよい。
6. `docs/` ルート直下の md は **README / DOCUMENTATION-STANDARD / development-guidelines** の 3 本に集約する。

執筆ルール（メタデータ・MUST/SHOULD 等）は [`DOCUMENTATION-STANDARD.md`](DOCUMENTATION-STANDARD.md)。コントリビュータ向け共通ルールは [`development-guidelines.md`](development-guidelines.md)。

---

## Quick Start

最短でスタックを起動し、1 回実行する:

→ **[guides/getting-started.md](guides/getting-started.md)**

---

## Guides（手順）

利用者の**第一入口**。仕様の前にここから始める。

### あなたがやりたいこと

| やりたいこと | ドキュメント |
| --- | --- |
| **初回セットアップ・起動** | [getting-started.md](guides/getting-started.md) |
| **HTTP で API を叩く** | [http-request-examples.md](guides/http-request-examples.md) |
| **定義を書く** | [concepts/definition.md](concepts/definition.md) → [specifications/definition.md](specifications/definition.md) |
| **実行する** | [getting-started.md](guides/getting-started.md)、[specifications/api-http.md](specifications/api-http.md) |
| **状態を確認する** | [ui-user-guide.md](guides/ui-user-guide.md)、[specifications/ui/visual.md](specifications/ui/visual.md) |
| **拡張する（Module / Engine）** | [engine-standalone-guide.md](guides/engine-standalone-guide.md)、[specifications/actions/module-zip-layout.md](specifications/actions/module-zip-layout.md) |
| **運用する** | [operations-docker.md](guides/operations-docker.md)、[operations-tenant-bootstrap.md](guides/operations-tenant-bootstrap.md) |

一覧: [guides/README.md](guides/README.md)

---

## Concepts（思想・全体像）

Guide のあと、なぜそうなっているかを理解する。

→ [concepts/README.md](concepts/README.md)

---

## Reference（調べ物）

辞書・一覧。契約の Normative 本文ではない。

| 内容 | ドキュメント |
| --- | --- |
| DB スキーマ | [database-schema.md](reference/database-schema.md) |
| ログキー | [logging-property-keys.md](reference/logging-property-keys.md) |
| OpenAPI / Scalar | [api-openapi.md](reference/api-openapi.md) |
| 環境変数（抜粋） | [environment-variables.md](reference/environment-variables.md) |
| HTTP エラーコード | [error-codes.md](reference/error-codes.md) |
| Permission keys | [permission-keys.md](reference/permission-keys.md) |

一覧: [reference/README.md](reference/README.md)

---

## Specifications（契約）

必須要件の正本。1 つの Concept が複数 Spec を束ねる場合がある。

| 領域 | ドキュメント |
| --- | --- |
| 定義 YAML | [definition.md](specifications/definition.md) |
| HTTP API | [api-http.md](specifications/api-http.md) |
| データ連携 | [data-integration.md](specifications/data-integration.md) |
| Engine 実行 | [execution/](specifications/execution/)（fsm, events-and-commands, wait-cancel, fork-join, execution-graph） |
| Action プラットフォーム | [actions/platform.md](specifications/actions/platform.md)、[module-zip-layout.md](specifications/actions/module-zip-layout.md) |
| セキュリティ・監査 | [platform/](specifications/platform/) |
| UI | [ui/visual.md](specifications/ui/visual.md)、[ui/push-api.md](specifications/ui/push-api.md) |

一覧: [specifications/README.md](specifications/README.md)

---

## Architecture（システム俯瞰）

| ドキュメント | 内容 |
| --- | --- |
| [overview.md](architecture/overview.md) | レイヤー・全体図 |
| [domain-model-boundaries.md](architecture/domain-model-boundaries.md) | 境界・正本の所在 |
| [repository-layout.md](architecture/repository-layout.md) | リポジトリ構成 |

一覧: [architecture/README.md](architecture/README.md)

---

## Decisions（ADR）

設計判断の理由。順次追加。

→ [decisions/README.md](decisions/README.md)

---

## Future（将来構想）

**未実装**の構想のみ。現行仕様と混同しないこと。

| ドキュメント | 内容 |
| --- | --- |
| [platform-architecture.md](future/platform-architecture.md) | プラットフォーム構成（将来構想） |

一覧: [future/README.md](future/README.md)

---

## 知識種別（現行採用）

固定仕様ではない。Tutorial / Cookbook / FAQ 等を将来追加してよい。

| 種別 | フォルダ | 問い |
| --- | --- | --- |
| Guides | `guides/` | どうやる？ |
| Concepts | `concepts/` | なぜ・全体の流れは？ |
| Architecture | `architecture/` | 何がどこにあり、どう流れる？ |
| Reference | `reference/` | この値・キーは何？ |
| Specifications | `specifications/` | 必須要件は何？ |
| Decisions | `decisions/` | なぜその設計にした？ |
| Future | `future/` | 将来の可能性は？ |

推奨読書順: **Quick Start → Guides → Concepts → Reference → Specifications → Architecture → Decisions → Future**
