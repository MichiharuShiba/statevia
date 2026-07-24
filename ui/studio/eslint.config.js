// @ts-check
/// <reference types="node" />
import path from "node:path";
import { fileURLToPath } from "node:url";
import eslint from "@eslint/js";
import { defineConfig } from "eslint/config";
import tseslint from "typescript-eslint";
import react from "eslint-plugin-react";
import reactHooks from "eslint-plugin-react-hooks";
import jsxA11y from "eslint-plugin-jsx-a11y";
import jsdoc from "eslint-plugin-jsdoc";
import globals from "globals";

const tsconfigRootDir = path.dirname(fileURLToPath(
    import.meta.url));

const exportJsdocContexts = [
    "ExportNamedDeclaration > FunctionDeclaration",
    "ExportNamedDeclaration > VariableDeclaration",
    "ExportDefaultDeclaration > FunctionDeclaration",
    "ExportNamedDeclaration > TSInterfaceDeclaration",
    "ExportNamedDeclaration > TSTypeAliasDeclaration",
    "ExportNamedDeclaration > ClassDeclaration",
];

/** AST selector fragment: literal value contains hiragana or katakana. */
const japaneseCharClassInLiteral = String.raw `[value=/[\u3040-\u309F\u30A0-\u30FF]/]`;

export default defineConfig({
    ignores: [
        ".next/**",
        "node_modules/**",
        "coverage/**",
        "**/dist/**",
        "postcss.config.js",
        "tailwind.config.js",
        "vitest.config.ts",
        "eslint.config.js",
        "playwright.config.ts",
        "e2e/**",
    ],
}, {
    files: ["**/*.{ts,tsx}"],
    extends: [
        eslint.configs.recommended,
        ...tseslint.configs.recommended,
        ...tseslint.configs.recommendedTypeChecked,
    ],
    languageOptions: {
        parserOptions: {
            projectService: true,
            tsconfigRootDir,
        },
        globals: {
            ...globals.browser,
            ...globals.node,
        },
    },
    plugins: {
        react,
        // eslint-plugin-react-hooks の flat configs 型と ESLint Plugin 型が不一致のため窄める
        "react-hooks": /** @type {import("eslint").ESLint.Plugin} */ (
            /** @type {unknown} */
            (reactHooks)
        ),
        "jsx-a11y": jsxA11y,
        jsdoc,
    },
    settings: {
        react: {
            version: "detect",
        },
    },
    rules: {
        ...react.configs.flat.recommended.rules,
        "react-hooks/rules-of-hooks": "error",
        "react-hooks/exhaustive-deps": "error",
        ...jsxA11y.configs.recommended.rules,
        "react/react-in-jsx-scope": "off",
        "react/prop-types": "off",
        "no-nested-ternary": "error",
        "@typescript-eslint/consistent-type-assertions": [
            "error",
            { assertionStyle: "as", objectLiteralTypeAssertions: "never" },
        ],
        "jsdoc/require-jsdoc": [
            "error",
            {
                publicOnly: true,
                require: {
                    FunctionDeclaration: false,
                    MethodDefinition: false,
                    ClassDeclaration: false,
                },
                contexts: exportJsdocContexts,
            },
        ],
        "jsdoc/require-description": "off",
        "jsdoc/require-param-description": "off",
        "jsdoc/require-returns-description": "off",
        "@typescript-eslint/no-unused-vars": [
            "error",
            {
                argsIgnorePattern: "^_",
                varsIgnorePattern: "^_",
                caughtErrorsIgnorePattern: "^_",
            },
        ],
    },
}, {
    files: ["app/**/*.tsx"],
    rules: {
        "no-restricted-syntax": [
            "error",
            {
                selector: `JSXText${japaneseCharClassInLiteral}`,
                message: "UI 文言は i18n 辞書（useUiText）経由にしてください。",
            },
            {
                selector: `JSXAttribute[name.name='aria-label'] > Literal${japaneseCharClassInLiteral}`,
                message: "aria-label は i18n 辞書経由にしてください。",
            },
            {
                selector: `JSXAttribute[name.name='title'] > Literal${japaneseCharClassInLiteral}`,
                message: "title 属性は i18n 辞書経由にしてください。",
            },
        ],
    },
}, {
    files: ["shared/i18n/uiText.ts", "shared/i18n/uiText.en.ts"],
    rules: {
        "jsdoc/require-jsdoc": "off",
        // 辞書合成のため shared/i18n のルート辞書のみ feature i18n 切片の import を許可する
        "no-restricted-imports": "off",
    },
}, {
    files: ["shared/**/*.{ts,tsx}"],
    ignores: ["shared/i18n/uiText.ts", "shared/i18n/uiText.en.ts"],
    rules: {
        "no-restricted-imports": [
            "error",
            {
                patterns: [{
                        group: ["@/features", "@/features/*"],
                        message: "shared は features を import してはいけません（依存方向: app → features → shared）。辞書合成は shared/i18n/uiText*.ts のみ例外。",
                    },
                    {
                        group: ["**/features/**", "features/**", "*/features/*"],
                        message: "shared は features を import してはいけません（依存方向: app → features → shared）。辞書合成は shared/i18n/uiText*.ts のみ例外。",
                    },
                ],
            },
        ],
    },
}, {
    files: ["tests/**/*.{ts,tsx}"],
    languageOptions: {
        globals: {
            ...globals.browser,
            ...globals.node,
            ...globals.vitest,
        },
    },
    rules: {
        "jsdoc/require-jsdoc": "off",
        "@typescript-eslint/require-await": "off",
        "@typescript-eslint/no-floating-promises": "off",
        "@typescript-eslint/consistent-type-assertions": "off",
        "@typescript-eslint/no-unsafe-assignment": "off",
        "@typescript-eslint/no-unsafe-return": "off",
    },
}, );