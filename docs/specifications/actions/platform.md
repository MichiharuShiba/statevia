# Action 実行プラットフォーム

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 1.0 |
| 更新日 | 2026-07-07 |
| Scope | Core-API / ModuleHost / Action 実行 |
| 関連 | [module-zip-layout.md](module-zip-layout.md), [../../concepts/actions.md](../../concepts/actions.md) |

---

Action Module の発見・登録・可視性・実行ポリシー・dispatch の Normative 概要。実装は `core/actions/Statevia.Core.Actions.Abstractions` と Core-API の composition root にある。

## Normative 要約

- **MUST**: 認可済みでもテナント境界を跨いで Action / Module メタデータを返してはならない。
- **MUST**: Policy で要求された実行モードを満たせない場合、安全側に失敗する（例: `ActionHostNotConfigured`）。
- **MUST**: 定義 publish 時に参照 actionId が Catalog に存在すること。
- **SHOULD**: 運用環境では Community Module を InProcess で実行しない。

---

## 1. コンポーネント責務

| コンポーネント | 責務 |
| --- | --- |
| **IActionCatalog** | actionId 解決・メタデータ保持 |
| **IActionVisibilityResolver** | テナント境界による Action の可視性 |
| **IActionExecutionPolicy** | TrustLevel × Environment × 階層下限から実行モードを決定（緩和不可） |
| **IActionExecutor** | Policy 結果に応じたバックエンドへの dispatch |
| **ModuleHost** | filesystem / OCI 等の Source から Module をロードし Catalog へ登録 |

Engine は **IStateExecutor** のみを呼び出す。Catalog / Policy / ModuleHost は Engine 非依存とする。

## 2. Module 供給

- 複数 **IModuleSource** を **CompositeModuleSource** が `Priority` 昇順で集約する
- 同名 Module は高優先 Source が勝つ（同 Priority は `SourceLabel` で tie-break）
- リモート Source（OCI 等）は acquire → cache → verify → extract → materialize の後、ローカル正本として discover する

設定例は `AGENTS.md` の `Statevia:Modules:*` を参照。

## 3. TrustLevel と署名

| 状態 | TrustLevel（例） |
| --- | --- |
| 署名なし | Community |
| 署名有効・信頼フィンガープリント一致 | Verified |
| 署名有効・未信頼 | Signed |
| 検証失敗 | Untrusted |

`Statevia:Modules:Signing:RequireSignature=true` のとき、署名なし Module の登録は skip する。

## 4. 実行モード

Policy が返しうるモード（実装状況はバージョンにより異なる）:

| モード | 概要 |
| --- | --- |
| InProcess | Core-API プロセス内 ALC |
| OutOfProcess | Action Host（gRPC）。`Statevia:ActionHost:BaseUrl` 必須 |
| Container / Wasm | サンドボックス runtime 委譲（Phase 4 以降） |
| Remote | 未登録時は UnsupportedExecutionMode |

テナントスコープの下限は `Statevia:ExecutionPolicy:Tenants:{tenantId}:MinimumMode` で設定可能。base より緩い指定は無視する。

## 5. MUST / SHOULD

- **MUST**: 認可済みでもテナント境界を跨いで Action / Module メタデータを返してはならない
- **MUST**: Policy で要求された実行モードを満たせない場合、安全側に失敗する（例: ActionHostNotConfigured）
- **MUST**: 定義 publish 時に参照 actionId が Catalog に存在すること（Compiler 照合）
- **SHOULD**: 運用環境では Community Module を InProcess で実行しない（OutOfProcess 以上を Policy で強制）

## 6. 関連ドキュメント

- zip レイアウト: [module-zip-layout.md](module-zip-layout.md)
- 署名運用: [../../guides/action-module-signing.md](../../guides/action-module-signing.md)
- Action Host: [../../guides/action-host.md](../../guides/action-host.md)
- 設計判断: [../../decisions/action-module-signing.md](../../decisions/action-module-signing.md)
