# Statevia とは

| 項目 | 値 |
| --- | --- |
| 種別 | Concept |
| Version | 1.0 |
| 更新日 | 2026-07-07 |
| 関連 | [definition.md](definition.md), [../architecture/overview.md](../architecture/overview.md) |

---

**Statevia** は **Definition Driven Execution Platform** です。ワークフロー定義（Definition）を正本とし、そこから実行・拡張・運用が一貫して派生します。

## なぜ定義駆動か

ビジネスプロセスは「いま何が起きているか」と「次に何が許されるか」の両方を扱う必要があります。Statevia はワークフロー定義を形式的な仕様として扱い、実行エンジンがその定義に従って状態遷移を進めます。設定ファイルの寄せ集めではなく、検証可能な契約として定義を publish し、実行はその版に固定されます。

## 設計原則

Statevia は次の原則に基づいて設計されています。

- **定義駆動型ワークフロー** — 実行は immutable な定義版に紐づく
- **事実駆動型 FSM** — 実際の結果（事実）のみが遷移をトリガーする
- **Fork / Join は制御ノード** — 状態そのものではなく、並列・合流の制御として扱う
- **明示的な依存関係** — 定義上で依存を宣言し、暗黙の順序に頼らない
- **協調的キャンセル** — 非同期実行を安全に止めるため、ユーザーコードがキャンセルを処理する
- **セーフティファースト** — 信頼境界・テナント境界・実行ポリシーを既定で厳しく保つ
- **非侵入型エンジン** — エンジンは状態実行を強制終了しない
- **観測と実行の分離** — 実行ロジックと read-model / 可視化の責務を分ける

## 事実駆動型遷移

キャンセル**要求**は事実ではありません。状態が協調的にキャンセルを処理し、完了・失敗などの**事実**が reducer に届いたとき初めて遷移が確定します。これにより、外部入力と内部状態の整合を保ちます。

## プラットフォームの構成

大まかな流れは次のとおりです。

1. **定義を書く** — YAML でワークフローを記述し Core-API に publish する
2. **実行を開始する** — API が Engine を起動し、永続化とイベント記録を行う
3. **Action で拡張する** — Module としてビジネスロジックを登録し、Policy に従って実行する
4. **観測する** — UI や API で実行グラフ・状態を read-model として参照する

レイヤー構成の詳細は [architecture/overview.md](../architecture/overview.md)。最短手順は [guides/getting-started.md](../guides/getting-started.md)。

## 次に読むもの

| テーマ | ドキュメント |
| --- | --- |
| 定義の考え方 | [definition.md](definition.md) |
| 実行の流れ | [execution-model.md](execution-model.md) |
| 拡張（Action） | [actions.md](actions.md) |
| 永続化・イベント | [durability.md](durability.md) |
| セキュリティ・境界 | [platform.md](platform.md) |

契約の正本は [specifications/](../specifications/README.md) を参照してください。
