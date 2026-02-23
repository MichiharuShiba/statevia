import type { Page } from "@playwright/test";

export class ExecutionPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/");
  }

  get heading() {
    return this.page.getByRole("heading", { name: "Execution UI" });
  }

  get healthLink() {
    return this.page.getByRole("link", { name: "health" });
  }

  get executionIdInput() {
    return this.page.getByLabel("Execution ID");
  }

  get loadButton() {
    return this.page.getByRole("button", { name: "Load" });
  }

  get cancelButton() {
    return this.page.getByRole("button", { name: "Cancel" });
  }

  get listViewButton() {
    return this.page.getByRole("button", { name: "List" });
  }

  get graphViewButton() {
    return this.page.getByRole("button", { name: "Graph" });
  }

  get fullscreenButton() {
    return this.page.getByRole("button", { name: "全画面表示" });
  }

  async expectHeaderVisible(): Promise<void> {
    await this.heading.waitFor({ state: "visible" });
    await this.healthLink.waitFor({ state: "visible" });
    await this.executionIdInput.waitFor({ state: "visible" });
    await this.loadButton.waitFor({ state: "visible" });
    await this.cancelButton.waitFor({ state: "visible" });
  }

  async loadExecution(): Promise<void> {
    await this.loadButton.click();
  }

  async waitForExecutionLoaded(options?: { timeout?: number; graphId?: string }): Promise<void> {
    const timeout = options?.timeout ?? 5000;
    await this.page.getByText("ACTIVE", { exact: true }).waitFor({ state: "visible", timeout });
    if (options?.graphId !== undefined) {
      await this.page.getByText(options.graphId, { exact: true }).first().waitFor({ state: "visible", timeout });
    }
  }

  getNodeListCell(nodeId: string) {
    return this.page.getByRole("cell", { name: nodeId, exact: true });
  }

  async switchToGraphView(): Promise<void> {
    await this.graphViewButton.click();
  }

  async waitForGraphView(options?: { timeout?: number }): Promise<void> {
    const timeout = options?.timeout ?? 3000;
    await this.fullscreenButton.waitFor({ state: "visible", timeout });
  }
}
