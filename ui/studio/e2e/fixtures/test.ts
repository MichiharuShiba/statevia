import { test as base } from "@playwright/test";
import { ExecutionPage } from "../pages/ExecutionPage";

type E2EFixtures = {
  executionPage: ExecutionPage;
};

export const test = base.extend<E2EFixtures>({
  executionPage: async ({ page }, use) => {
    await use(new ExecutionPage(page));
  },
});

export { expect } from "@playwright/test";
