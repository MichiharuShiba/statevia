# E2E テスト (Playwright)

**Fixture + Page Object** で構成しています。

- **Page Object**: 画面のロケーターと操作は `pages/ExecutionPage.ts` に集約
- **Fixture**: `fixtures/test.ts` で `executionPage` を注入。各テストで `new ExecutionPage(page)` を書かず、`async ({ executionPage }) => { ... }` で利用

## 初回セットアップ

```bash
npm install
npx playwright install
```

## 実行

- ヘッドレスで実行: `npm run test:e2e`
- UI モードで実行: `npm run test:e2e:ui`

`services/ui` から実行してください。テスト実行時に `next dev` が自動で起動します。

通常のスペック（`smoke.spec.ts` / `execution.spec.ts` など）はブラウザ側で API をモックしているため、**Core-API の起動は不要**です。

### Core-API 実体を使うオプション E2E（STV-401 / STV-402）

事前に **PostgreSQL と Core-API**（例: `http://localhost:8080`）を起動し、マイグレーション済みであることを前提とします。

環境変数 **`CORE_API_E2E_URL`** に Core-API のオリジンを設定すると、`core-api-real.spec.ts` と `core-api-ui-workflow.spec.ts` が実行されます（未設定のときこれらはスキップ）。

**PowerShell の例**

```powershell
cd services/ui
$env:CI = "true"
$env:CORE_API_E2E_URL = "http://localhost:8080"
npx playwright test e2e/core-api-real.spec.ts
npx playwright test e2e/core-api-ui-workflow.spec.ts
# 両方まとめて
npx playwright test e2e/core-api-real.spec.ts e2e/core-api-ui-workflow.spec.ts
```

**bash の例**

```bash
cd services/ui
CORE_API_E2E_URL=http://localhost:8080 npx playwright test e2e/core-api-real.spec.ts
CORE_API_E2E_URL=http://localhost:8080 npx playwright test e2e/core-api-ui-workflow.spec.ts
```

`playwright.config.ts` は `CORE_API_E2E_URL` があるとき、Next のプロキシ用に **`CORE_API_INTERNAL_BASE`** を同じ値で渡します。別 URL にしたい場合は **`CORE_API_INTERNAL_BASE`** を明示してください。

`CORE_API_E2E_URL` を張った状態で `npm run test:e2e` を叩くと、モック E2E と実 API E2Eが混在するため負荷が上がります。トラブル時は上記のようにスペックを限定するか、一時的に `CORE_API_E2E_URL` を外してください。
