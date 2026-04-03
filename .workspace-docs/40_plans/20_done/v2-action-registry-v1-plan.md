# Action Registry v1 実装計画

Version: 0.1  
Target: v1.0.0  
Scope: Core-API（同一リポジトリ・同期実行）

---

## 0. 目的

States 形式 YAML に `action` を定義し、**登録済みの具象ロジック**を実行できるようにする。  
v1 では以下を前提とする。

- 実行は **同期**（同プロセス）
- モジュールは **同一リポジトリ**
- 未登録 action は **定義登録時エラー**

---

## 1. 非目標（v1 ではやらない）

- action-runner サービスの物理分離
- Queue 経由の非同期 action 実行
- 外部 Module Registry / 署名検証
- マルチリポジトリ運用

---

## 2. v1 アーキテクチャ

### 2.1 役割分担

- **Engine**
  - `IStateExecutor` 実行に専念
  - action 名解決はしない
- **Core-API**
  - 定義読み込み時に `action` を検証
  - `actionId -> IStateExecutor` を Registry で解決

### 2.2 コンポーネント

- `IActionRegistry`
  - `bool TryResolve(string actionId, out IStateExecutor executor)`
  - `bool Exists(string actionId)`
- `InMemoryActionRegistry`
  - DI 登録済み action をメモリ保持
- `ActionExecutorFactory`（`IStateExecutorFactory`）
  - stateName から state 定義を見て `action` を引き、Registry から executor を解決
- `DefinitionCompilerService`
  - YAML 読み込み・検証・コンパイル（未登録 action はここで失敗）

---

## 3. YAML 仕様（v1）

```yaml
workflow:
  name: OrderFlow

states:
  CreateOrder:
    action: order.create
    on:
      Completed: { next: Notify }

  Notify:
    action: notify.email
    input: $.result
    on:
      Completed: { end: true }
```

### 3.1 ルール

- `states.<state>.action` は任意（`wait` のみ状態は省略可）
- `action` 指定時、登録済み actionId であること
- 未登録は定義登録時にエラー（422 相当）

---

## 4. フォルダ構成（v1）

```text
api/Statevia.Core.Api/
  Application/
    Actions/
      Abstractions/
        IActionRegistry.cs
      Registry/
        InMemoryActionRegistry.cs
      Builtins/
        NoOpActionExecutor.cs
        WaitActionExecutor.cs
    Definition/
      ActionExecutorFactory.cs
  Hosting/
    DefinitionCompilerService.cs
```

---

## 5. 実装タスク

| ID | タスク | 完了条件 |
|----|--------|----------|
| AR-1（完了） | 定義モデルに `action` を追加 | `StateDefinition.Action` を保持・ローダーで読める |
| AR-2（完了） | Registry 契約を追加 | `IActionRegistry` / `InMemoryActionRegistry` が存在 |
| AR-3（完了） | Action 解決ファクトリ実装 | `ActionExecutorFactory` で stateName->action->executor 解決 |
| AR-4（完了） | 定義登録時検証 | 未登録 action を `DefinitionCompilerService` でエラー化 |
| AR-5（完了） | Built-in action 移行 | 既存 NoOp/Wait を Registry 登録経由に変更 |
| AR-6（完了） | 単体テスト | 正常解決、未登録失敗、wait 互換を追加 |
| AR-7（完了） | ドキュメント同期 | `workflow-definition-spec` と `core-engine-definition-spec` に `action` 追記 |

---

## 6. エラーポリシー

- エラー種別: `UnknownAction`
- メッセージ例:
  - `Unknown action 'order.create' in state 'CreateOrder'.`
- 返却ステータス: 422（定義不正）

---

## 7. テスト観点

- `action` が登録済みならコンパイル成功
- 未登録 `action` ならコンパイル失敗（エラー文言確認）
- `wait` 状態の既存挙動が変わらない
- `input`（マッピング）との併用で executor に input が渡る

---

## 8. 将来拡張への接続点

v2 以降で `InMemoryActionRegistry` を以下に差し替えるだけで拡張できる。

- `RemoteActionRegistry`（別サービス問い合わせ）
- `CachedActionRegistry`（メタデータキャッシュ）
- `ModuleBackedActionRegistry`（署名済みモジュール解決）

---

## 9. 変更履歴

| 版 | 日付 | 内容 |
|----|------|------|
| 0.1 | 2026-03 | 初版（v1 同期・同一repo・登録時エラー方針） |
| 0.2 | 2026-03 | 実装反映（AR-1〜AR-7: Engine に `action` ロード・L1、API に Registry / `ActionExecutorFactory` / 組み込み `noop`、仕様同期、テスト） |
