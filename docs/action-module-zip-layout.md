# Action Module zip の構成と展開結果

Version: 1.0  
Project: 実行型ステートマシン

---

CLI の `statevia module install` は `ModuleZipInstaller`（`shared/Statevia.Modules`）で zip を **modules ルート**配下へ展開する。  
展開後のディレクトリ構成が Core-API の filesystem scan（`FilesystemModuleSource`）と一致している必要がある。

関連:

- 配置・reload 手順: `docs/operations-docker.md`
- modules ルート解決: `STATEVIA_MODULES_PATH` / `Statevia:Modules:Path`（`AGENTS.md`）

---

## 1. 展開後の正しい構成（modules ルート基準）

展開が成功し **load まで通る**最小構成は次のとおり。

```text
{modulesRoot}/
└─ {moduleDirectoryName}/          # 1 module = 1 ディレクトリ
   ├─ {moduleDirectoryName}.dll     # 推奨: entry assembly（IActionModule 実装）
   ├─ *.dll                         # 任意: 私有依存（ホスト未共有分のみ）
   └─ {moduleDirectoryName}.deps.json  # 任意: 依存解決用（publish 成果物に含まれる場合）
```

### entry assembly の解決規則

Core-API は module ディレクトリの **直下**（サブフォルダは見ない）だけを entry 候補とする。

| 条件 | 結果 |
|------|------|
| `{moduleDirectoryName}.dll` が存在 | それを entry とする |
| 直下に `*.dll` が 1 つのみ | それを entry とする |
| 直下に `*.dll` が 0 または 2 以上（かつ `{name}.dll` なし） | **skip**（load されない） |

**推奨**: ディレクトリ名と同名の `{moduleDirectoryName}.dll` を zip に含める。

---

## 2. zip の入力パターン（展開前）

パス区切りは `/` でも `\` でもよい。ディレクトリのみの entry（末尾 `/`）は無視される。

### 2.1 パターン A — 単一トップレベルディレクトリ（推奨）

zip 内のトップレベルが **1 ディレクトリだけ** のとき、その名前が `{moduleDirectoryName}` になる。  
展開時に zip 内の **同名プレフィックスは剥がされる**（二重ネスト防止）。

**zip 内:**

```text
order.module.zip
└─ order.module/
   ├─ order.module.dll
   ├─ SomePrivateLib.dll
   └─ order.module.deps.json
```

**展開後（`--modules-path ./modules` の例）:**

```text
modules/
└─ order.module/
   ├─ order.module.dll
   ├─ SomePrivateLib.dll
   └─ order.module.deps.json
```

### 2.2 パターン B — ルート直下にファイル（フラット zip）

zip ルートに複数ファイルがある、またはトップレベルが複数種類あるとき、  
**zip ファイル名（拡張子除く）** が `{moduleDirectoryName}` になる。

**zip 内:**

```text
notify.module.zip
├─ notify.module.dll
└─ notify.module.deps.json
```

**展開後:**

```text
modules/
└─ notify.module/
   ├─ notify.module.dll
   └─ notify.module.deps.json
```

### 2.3 パターン C — 複数トップレベルディレクトリ（非推奨）

トップレベルディレクトリが複数ある zip は zip ベース名のフォルダへ展開されるが、  
サブフォルダ内だけに DLL があると entry が解決できず **load されない**。

```text
# 展開は成功するが load 対象外になりやすい例
bad.module.zip
├─ alpha/alpha.dll
└─ beta/beta.dll
        ↓
modules/bad.module/alpha/alpha.dll   # 直下に entry DLL なし → skip
```

---

## 3. zip に含めるファイル

| 種別 | 含める | 備考 |
|------|--------|------|
| `{moduleDirectoryName}.dll` | **必須**（推奨） | `IActionModule` 実装 |
| 私有 NuGet 依存の `*.dll` | 必要なら | module ディレクトリ直下 |
| `{moduleDirectoryName}.deps.json` | publish 時にあれば | `AssemblyDependencyResolver` が参照 |
| `Statevia.Modules` 等の共有アセンブリ | **不要** | ホスト ALC が Default から共有（同梱しても動作はするが冗長） |

ホストが共有するアセンブリ（同梱不要）:

- `Statevia.Modules`
- `Statevia.Core.Actions.Abstractions`
- `Statevia.Core.Engine`
- `Microsoft.Extensions.DependencyInjection.Abstractions`

---

## 3.1 署名ファイル（任意・Trust 判定用）

Module ディレクトリ直下に detached 署名ファイル `{moduleDirectoryName}.signature.json` を置くと、Core-API は登録時に署名を検証して信頼レベル（`TrustLevel`）を決定する。署名がない Module は従来どおり `Community` として登録される。発行者・運用者の運用手順は `docs/action-module-signing.md` を参照。

```json
{
  "algorithm": "RSA-SHA256",
  "publicKeyPem": "-----BEGIN PUBLIC KEY-----\n...\n-----END PUBLIC KEY-----",
  "signatureBase64": "<entry DLL の SHA-256 への署名(Base64)>",
  "signerName": "Statevia Official"
}
```

| フィールド | 必須 | 説明 |
|------------|------|------|
| `algorithm` | 必須 | 現状 `RSA-SHA256` のみ対応 |
| `publicKeyPem` | 必須 | 署名者公開鍵（SubjectPublicKeyInfo, PEM） |
| `signatureBase64` | 必須 | **entry DLL バイト列の SHA-256** への署名 |
| `signerName` | 任意 | **自己申告・表示専用**。信頼判定には使われない |

### 信頼レベルの決定

| 状態 | TrustLevel |
|------|------------|
| 署名ファイルなし | `Community`（`Statevia:Modules:Signing:RequireSignature=true` のときは登録 skip） |
| 署名有効・公開鍵フィンガープリントが許可集合内 | `Verified` |
| 署名有効・公開鍵フィンガープリントが許可集合外 | `Signed`（改ざんなしのみ保証） |
| 署名不正・破損・未対応アルゴリズム | `Untrusted` |

- 許可集合は `Statevia:Modules:Signing:TrustedSignerFingerprints`（公開鍵 SubjectPublicKeyInfo の SHA-256 フィンガープリント、16 進）。
- フィンガープリントは `publicKeyPem` から再計算した値を唯一の根拠とし、manifest 申告値は信頼しない。
- **`Signed` は「改ざんされていないこと」のみ保証**し、運営が署名者を信頼した状態（`Verified`）とは異なる。UI 表示時は `TrustLevel` とセットで提示する。

### スコープと将来拡張

- 現状の署名対象は **entry DLL 単体**であり、**同梱した依存 DLL の差し替えは検知できない**。将来は「ファイル一覧 + 各 SHA-256 の manifest」を署名対象に拡張予定。
- 鍵漏洩時のローテーションに備え、将来 `RevokedSignerFingerprints`（失効リスト）を追加できる構造とする。

---

## 4. 展開サイズ上限

| 制限 | 値 |
|------|-----|
| 1 entry（1 ファイル）の展開後サイズ | **32 MiB** |
| zip 全体の展開後合計 | **64 MiB** |

超過時は `InvalidOperationException`。大容量モデルやメディアは module zip ではなく Object Storage 等へ置く。

---

## 5. 拒否される zip（展開失敗）

| 例 | 理由 |
|----|------|
| 空 zip | エントリなし |
| `../escape.dll` | modules ルート外への解決 |
| `mod/./x.dll` / `mod/lib/../../x.dll` | `.` / `..` セグメント |
| 32 MiB 超の単一ファイル | entry 上限 |
| 合計 64 MiB 超 | archive 上限 |
| 非圧縮サイズ未知の entry | サイズ検証不能 |

---

## 6. 上書きと reload

- 同一 `{moduleDirectoryName}` が既にある場合、**ディレクトリごと削除してから**再展開する。
- Core-API が新 DLL を読み込むには **再起動** または `POST /internal/modules/reload` が必要（`docs/operations-docker.md`）。

---

## 7. 作成例（パターン A）

```bash
# publish 成果物から zip を作る例（ディレクトリ名 = my.module）
dotnet publish path/to/MyModule.csproj -c Release -o ./publish/my.module
cd ./publish && zip -r ../my.module.zip my.module
statevia module install ../my.module.zip --modules-path ./modules --api-base http://localhost:8080 --token "<jwt>"
```

Windows で zip ツールが無い場合は、エクスプローラで `my.module` フォルダを右クリック圧縮し、**ルートが `my.module/` 1 本**になるよう調整する。

---

## 8. 実装参照

| 処理 | 実装 |
|------|------|
| zip 展開・ディレクトリ名決定 | `shared/Statevia.Modules/ModuleZipInstaller.cs` |
| entry DLL 解決 | `api/Statevia.Service.Api/Application/Actions/Modules/FilesystemModuleSource.cs` |
| ALC・共有アセンブリ | `shared/Statevia.Modules/ModuleAssemblyLoadContext.cs` |
| 署名検証・Trust 判定 | `api/Statevia.Service.Api/Application/Actions/Modules/ModuleSignatureVerifier.cs` |
| 単体テスト（展開パターン） | `shared/Statevia.Modules.Tests/ModuleZipInstallerTests.cs` |
