# Architecture

| 項目 | 値 |
| --- | --- |
| 種別 | Architecture |
| Version | 1.1 |
| 更新日 | 2026-07-23 |

---

**現行システム** のレイヤー・データフロー・コンポーネント構成。思想（Concept）や契約（Specification）とは別の俯瞰用ドキュメント。

## ドキュメント

| ドキュメント | 内容 |
| --- | --- |
| [`overview.md`](overview.md) | レイヤー・全体図・Docker 構成 |
| [`domain-model-boundaries.md`](domain-model-boundaries.md) | ドメイン境界・正本の所在 |
| [`repository-layout.md`](repository-layout.md) | リポジトリディレクトリ構成 |
| [`ui-studio-structure.md`](ui-studio-structure.md) | Studio 内部（`app` / `features` / `shared`） |

将来構想は [`../future/`](../future/) に隔離する（現行 Architecture に混ぜない）。

入口は [`../README.md`](../README.md) の Architecture セクション。
