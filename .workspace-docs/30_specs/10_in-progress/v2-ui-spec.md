# UI 仕様（v2）

- Version: 1.0.0
- 更新日: 2026-04-02
- 対象: v2 スタック向け UI（Core-API C# + Engine C#）
- 関連: `docs/core-api-interface.md`、`AGENTS.md`

---

Statevia UI は、v2 スタック（Core-API C# + Engine C#）向けに、ワークフローの編集と可視化を提供する。

---

## 画面

### Definition Editor

機能:

- nodes YAML の編集（`version: 1`、`workflow`、`nodes`）
- ワークフローの検証（サーバー側コンパイル経路）
- 定義の保存
- フィールド単位のヒント付きエラー表示

検証メモ（v2）:

- 1 つのドキュメント内で `nodes` と `states` の併存は不可。
- nodes グラフでは `type: end` は必ず 1 つ。
- `input` マッピングのパスは `$` / `$.seg1.seg2` 形式のみ許可。
- `${...}` テンプレート構文は不正として扱い、エディタ上でエラー表示する。

---

### Workflow Runner

機能:

- ワークフロー開始
- ワークフローキャンセル
- イベント送信
- コマンド結果と最新状態（`Running` / `Completed` / `Failed` / `Cancelled`）の表示

API 連携（v2）:

- Definitions: `POST /v1/definitions/validate`, `POST /v1/definitions`
- Workflows: `POST /v1/workflows`, `POST /v1/workflows/{id}/events`, `POST /v1/workflows/{id}/cancel`

---

### Execution Graph Viewer

ExecutionGraph を可視化する。

ノード状態:

```text

Running
Completed
Failed
Cancelled

```

エッジ種別:

```text

Next
Fork
Join

```

---

## 表示ルール

Fork / Join

- 視覚的にグルーピングする
- 並列ブランチを揃えて配置する

Wait / Resume / Cancel

- 強調表示する

入力 / 出力の表示

- `workflowInput` とノード `output` は機微情報を含む可能性がある
- UI はデフォルトで大きなペイロードをマスクまたは折りたたむ
- 生データのコピーは明示的なユーザー操作を必須にする

Running ノード

- 目立ちすぎないアニメーションを適用する

Failed / Cancelled

- 強い色で表示する

---

## Node Inspector

表示項目:

- state 名
- fact
- output
- timestamps
- 遷移種別（`Next` / `Fork` / `Join`）
- wait 状態のイベント名

---

## 変更履歴

| 版 | 日付 | 内容 |
|----|------|------|
| 1.0.0 | 2026-04-02 | 初版としてメタブロックを付与（本文は既存のまま）。 |
