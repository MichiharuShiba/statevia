# ドキュメント執筆標準

| 項目 | 値 |
| --- | --- |
| 種別 | 参照（執筆者向け） |
| Version | 1.0 |
| 更新日 | 2026-07-07 |

---

本文書は **Markdown の書き方** の正本である。知識体系・索引・学習導線は [`README.md`](README.md) を参照すること。

## メタブロック

`docs/` 配下の各 Markdown（本書・`README.md` を除く）のタイトル直後に次を置く。

```markdown
| 項目 | 値 |
| --- | --- |
| 種別 | Guide / Concept / Architecture / Reference / Specification / Decision / Future |
| Version | x.y |
| 更新日 | YYYY-MM-DD |
| 関連 | [他 doc へのリンク] |
```

- **変更履歴**: 重要な変更は `**Version x.y（日付）**: 概要` を最大 5 件まで
- **種別**の定義一覧は [`README.md`](README.md) に委譲する

## 配置ルール

- **新規 doc は `docs/` ルート直下に作らない。** 種別に対応するサブフォルダへ置く
- ルートに置いてよいのは `README.md`、`DOCUMENTATION-STANDARD.md`、`development-guidelines.md` のみ

## 命名

- ファイル名は **kebab-case**、拡張子 `.md`
- 意味が伝わる短い名（例: `getting-started.md`, `execution-model.md`）

## 規範表現（Specification）

契約・仕様（**Specification**）では RFC 2119 風に次を用いる。

| 表現 | 意味 |
| --- | --- |
| **必須** | 実装・利用者が満たさなければならない |
| **推奨** | 強く望ましいが例外があり得る |
| **任意** | 省略可能 |
| **禁止** | してはならない |

Guide / Concept / Architecture / Reference / Decision / Future では MUST/SHOULD 等の規範キーワードを使わない。

## 叙述と契約の分離

- 背景・思想・「なぜ」→ **Concept**
- 手順 → **Guide**
- 一覧・辞書 → **Reference**
- 振る舞い・契約 → **Specification**
- 設計判断の理由 → **Decision（ADR）**

仕様 PR で長い経緯節を増やさない。必要なら Concept または ADR へ書く。

## Markdown

- 本文は日本語（テンプレ由来の英語見出しは維持可）
- [`.markdownlint.json`](../.markdownlint.json) に準拠する
- 見出し・リスト・コードブロックの前後に空行を入れる
- **Mermaid**（`flowchart`）: エッジラベルの可読性調整が必要なときは、ラベル文字列の左右に `&nbsp;&nbsp;` を付け、同一図内で統一する

## 編集後の確認

```bash
npx markdownlint-cli2 "docs/<変更したファイルパス>"
```

## リンク

- `docs/` 内の相互リンクは相対パスを用いる
- **利用者向け `docs/` から内部作業用 spec フォルダへリンクしない**（完了内容は `docs/` へ昇格する）
