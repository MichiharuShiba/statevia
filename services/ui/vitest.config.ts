import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    globals: true,
    environment: "jsdom",
    include: ["tests/**/*.test.ts", "tests/**/*.test.tsx"],
    setupFiles: ["./tests/setup.ts"],
    coverage: {
      provider: "v8",
      reporter: ["text", "json", "html", "lcov"],
      exclude: [
        "node_modules/",
        ".next/",
        "**/*.test.ts",
        "**/*.test.tsx",
        "**/*.config.ts",
        "app/lib/uiText.ts",
        "app/lib/uiText.en.ts",
        "app/lib/types.ts",
        "app/graphs/types.ts",
        "app/layout.tsx",
        "app/**/page.tsx",
        "app/components/editor/DefinitionGraphEditor.tsx",
        "app/components/editor/YamlCodeEditor.tsx",
        "app/components/editor/ActionInputCodeEditor.tsx",
        "app/components/execution/ExecutionDashboard.tsx",
        "app/definitions/DefinitionEditorPageClient.tsx",
        "app/components/nodes/NodeGraphView.tsx",
        "tests/**"
      ]
    }
  }
});
