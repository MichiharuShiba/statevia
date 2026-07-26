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
        "shared/i18n/uiText.ts",
        "shared/i18n/uiText.en.ts",
        "shared/api/apiError.ts",
        "shared/auth/authMe.ts",
        "shared/ui/ActionInputCodeEditor.tsx",
        "features/definitions/i18n/**",
        "app/lib/types.ts",
        "app/graphs/types.ts",
        "app/layout.tsx",
        "app/**/page.tsx",
        "features/definition-editor/ui/DefinitionGraphEditor.tsx",
        "features/definition-editor/ui/YamlCodeEditor.tsx",
        "features/executions/ui/ExecutionDashboard.tsx",
        "features/definition-editor/ui/DefinitionEditorPageClient.tsx",
        "features/executions/ui/NodeGraphView.tsx",
        "tests/**",
      ],
    },
  },
});
