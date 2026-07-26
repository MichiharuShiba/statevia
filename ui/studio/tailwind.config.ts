import type { Config } from "tailwindcss";

export default {
  // feature-first: ユーティリティクラスは app だけでなく features / shared にもある
  content: [
    "./app/**/*.{ts,tsx}",
    "./features/**/*.{ts,tsx}",
    "./shared/**/*.{ts,tsx}",
  ],
  theme: {
    extend: {}
  },
  plugins: []
} satisfies Config;
