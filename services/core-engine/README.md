# Statevia Core Engine

V2 アーキテクチャにおける Decide 専用の C# 独立サービス。DB を持たず、`POST /internal/v1/decide` を提供する。

## レイヤー構成

| プロジェクト | 責務 |
|-------------|------|
| **Statevia.CoreEngine.Domain** | ExecutionState / NodeState、Events、Reducer（Cancel wins + normalize） |
| **Statevia.CoreEngine.Application** | Decide UseCase、Guards（terminal / cancelRequested 等） |
| **Statevia.CoreEngine.Transport.Http** | HTTP Adapter（/internal/v1/decide の JSON 入出力） |

### Domain のフォルダ・名前空間（core/ に準拠）

フォルダ＝名前空間の対応で、責務ごとに分割している。

- **Execution/** → `Statevia.CoreEngine.Domain.Execution`  
  - `ExecutionStatus`, `ExecutionState`（実行集約の状態）
- **Node/** → `Statevia.CoreEngine.Domain.Node`  
  - `NodeStatus`, `NodeState`（ノードの状態）
- **Events/** → `Statevia.CoreEngine.Domain.Events`  
  - `Actor`, `ActorKind`, `EventEnvelope`, `EventTypeConstants`（固定 24 種）。core-events-spec 準拠。
- **Reducer/** → `Statevia.CoreEngine.Domain.Reducer`  
  - `ExecutionReducer.Reduce`（Cancel wins + normalize）。core-reducer-spec 準拠。
- **Extensions/** → `Statevia.CoreEngine.Domain.Extensions`  
  - `PayloadDictionaryExtensions`（AsPayloadDictionary, GetString, GetInt, GetObject）、`ExecutionStateExtensions`（IsCancelRequested, WithNode, WithNodeUpdated）、`NodeStatusExtensions`（IsActive）。
- ルート → `Statevia.CoreEngine.Domain`  
  - アセンブリマーカー等。

参照: `docs/architecture.v2.md`、改修タスク `.exclude/refactoring-tasks-v2.md` フェーズ1（1.1–1.9）。

## ビルド・実行

```bash
cd services/core-engine
dotnet build
dotnet run --project src/Statevia.CoreEngine.Transport.Http
```

デフォルトは `http://localhost:5000`（HTTPS は launchSettings 参照）。タスク 1.7 で `/internal/v1/decide` を実装予定。
