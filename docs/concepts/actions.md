# Action と Module

| 項目 | 値 |
| --- | --- |
| 種別 | Concept |
| Version | 1.0 |
| 更新日 | 2026-07-07 |
| 関連 | [../specifications/actions/](../specifications/actions/) |

---

**Action** はワークフロー状態に紐づく実行可能な単位です。**Module** は Action を束ねた配布単位で、zip として配置・署名・信頼レベル付与の対象になります。

## ストーリー: 拡張の流れ

1. 開発者が Module をビルドし、`module.json` と DLL を zip にまとめる
2. Core-API の modules ルート（または OCI Source）へ配置する
3. **ModuleHost** がロードし **Catalog** に `ActionDescriptor` を登録する
4. 定義 publish 時に Action ID が Catalog と照合される
5. 実行時 **Policy** が TrustLevel・環境・テナント下限から実行モード（InProcess / OutOfProcess / Container 等）を決定し、**Executor** がバックエンドへ dispatch する

Engine は `IStateExecutor` のみを知り、Catalog や ModuleHost には依存しません。拡張点は Core-API の composition root に閉じ込められます。

## 信頼と実行モード

署名の有無・検証結果により Module の TrustLevel が決まります。Policy は base 下限とテナント等の階層下限を合成し、**緩和はできません**。Community Module を本番で OutOfProcess にするなど、運用意図を設定で表現します。

OutOfProcess 実行には **Action Host**（gRPC）が必要です。未設定時は安全側に失敗します。

## 次に読むもの

| 内容 | ドキュメント |
| --- | --- |
| zip レイアウト | [specifications/actions/module-zip-layout.md](../specifications/actions/module-zip-layout.md) |
| Catalog / Policy / OCI | [specifications/actions/platform.md](../specifications/actions/platform.md) |
| Module 署名運用 | [guides/action-module-signing.md](../guides/action-module-signing.md) |
| Action Host 起動 | [guides/action-host.md](../guides/action-host.md) |
