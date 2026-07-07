# Action Module 署名と TrustLevel

| 項目 | 値 |
| --- | --- |
| 種別 | Decision |
| 更新日 | 2026-07-07 |
| ステータス | 採用 |

## Context

第三者 Module を同一プロセスで実行すると、テナント境界を跨ぐリスクやコード改ざんのリスクがある。署名なしでも開発利便性は必要。

## Decision

- Module zip は **任意署名**。検証結果で **TrustLevel**（Community / Signed / Verified / Untrusted）を決定する
- **信頼フィンガープリント**は設定 `Statevia:Modules:Signing:TrustedSignerFingerprints` で明示列挙する
- **Execution Policy** は TrustLevel × Environment × テナント下限から実行モードを決め、**base より緩和しない**
- `RequireSignature=true` のとき署名なし Module は登録を skip する

## Consequences

- 運用者は Policy と署名設定で本番の隔離レベルを制御できる
- 署名鍵管理・フィンガープリント運用の負荷が増える
- OutOfProcess / Container 等のバックエンド未構成時は安全側に失敗する

関連: [platform.md](../specifications/actions/platform.md)、[action-module-signing.md](../guides/action-module-signing.md)
