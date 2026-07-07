# Fork / Join 仕様

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 1.0 |
| 更新日 | 2026-07-07 |
| 関連 | [fsm.md](fsm.md) |

---

## Normative 要約

- **MUST**: Fork / Join は**制御ノード**であり、通常の状態（State）ではない。
- **MUST**: Join は依存する全分岐から所定の事実が揃うまで次へ進んではならない。
- **SHOULD**: Fork は定義上で並列開始する状態集合を明示する。

---

## Fork

Fork は複数の状態を同時に開始します。

例：A -> Fork -> [B, C]

## Join

Join は複数の状態からの事実を待ってから次に進みます。

例：Join(E) = all(B, C)

## ルール

- Fork と Join は状態ではありません。
- Join は依存状態が事実を生成するたびに評価されます。
- 必須状態のいずれかが失敗またはキャンセルされた場合、Join は失敗します。
- Fork は実行順序を保証しません。

## ネストされた Fork / Join

Fork と Join はネストして組み合わせることができます。Join ノードは他の Join ノードに依存することができます。
