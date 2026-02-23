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

テスト実行時に `next dev` が自動で起動します。API はテスト内でモックしているため、core-api の起動は不要です。
