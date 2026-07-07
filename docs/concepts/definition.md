# 定義（Definition）の考え方

| 項目 | 値 |
| --- | --- |
| 種別 | Concept |
| Version | 1.0 |
| 更新日 | 2026-07-07 |
| 関連 | [../specifications/definition.md](../specifications/definition.md) |

---

ワークフロー定義は、Statevia における**唯一の設計正本**です。実行中に定義を書き換えるのではなく、版を追加（publish）し、各実行は特定の `definition_version_id` に固定されます。

## ストーリー: 定義から実行へ

1. 作者は **states 形式**または **nodes 形式**でワークフローを記述する
2. Core-API が構文検証・Action 解決・コンパイルを行い、immutable な版として保存する
3. クライアントが `POST /v1/executions` を呼ぶと、その時点の最新版（または指定版）が実行にバインドされる
4. Engine はコンパイル済みグラフに従い、各状態で Action をスケジュールし、事実に応じて遷移する

nodes 形式はエディタ向けの表現であり、実行前に states 形式へ正規化される想定です。正本のフィールド意味・制約は Specification に委譲します。

## Action 参照

状態は任意で `action` を持ちます。省略時は組み込み noop と同等です。Module alias（`workflow.modules`）により、短い名前から canonical actionId へ解決されます。未登録の Action は publish 時にエラーとなり、実行開始前に問題を検出できます。

## 版と互換

定義は **append-only** で版管理されます。既存版を上書きせず、新しい `version` を追加します。これにより監査・再現・ロールバックの議論が「どの版で動いたか」に集約されます。

版レンジ解決（`@LATEST` 等）は設計済みで段階的に導入予定です。現行の Normative 契約は [definition 仕様](../specifications/definition.md) を参照してください。

## 次に読むもの

- 定義 YAML の必須要件: [specifications/definition.md](../specifications/definition.md)
- HTTP での publish: [specifications/api-http.md](../specifications/api-http.md)
- 実行モデル: [execution-model.md](execution-model.md)
