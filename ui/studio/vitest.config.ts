import path from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

const studioRoot = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  resolve: {
    alias: {
      "@/features": path.join(studioRoot, "features"),
      "@/shared": path.join(studioRoot, "shared"),
    },
  },
  test: {
    globals: true,
    environment: "jsdom",
    include: ["tests/**/*.test.ts", "tests/**/*.test.tsx"],
    setupFiles: ["./tests/setup.ts"],
    coverage: {
      provider: "v8",
      reporter: ["text", "json", "html", "lcov"],
      include: [
        "app/**/*.{ts,tsx}",
        "features/**/*.{ts,tsx}",
        "shared/**/*.{ts,tsx}",
        "middleware.ts",
      ],
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
        "features/scaffold.ts",
        "shared/scaffold.ts",
        "tests/**",
      ],
    },
  },
});
