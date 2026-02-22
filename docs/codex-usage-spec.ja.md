# CODEX 利用仕様書（プロジェクト運用）

Version: 1.0
Project: 実行型ステートマシン

## 1. 目的

本書は、OpenAI Codex（Codex App / Codex CLI / Codex Web）を用いて、
本リポジトリの実装・仕様更新・テスト・PR作成を行う際の **運用ルール** を定義する。

## 2. 対象ツール

- Codex CLI（ローカル端末で動作し、選択ディレクトリのコードを読み/変更/実行できる） :contentReference[oaicite:0]{index=0}
- Codex App（複数エージェント並列、worktree、git機能等を備えたデスクトップ体験） :contentReference[oaicite:1]{index=1}
- Codex Web（ChatGPT内からCodexタスクを割り当て、隔離環境で処理） :contentReference[oaicite:2]{index=2}

## 3. 基本方針

### 3.1 スレッド（Thread）運用

- 1スレッド＝1作業単位（仕様→実装→テスト→差分確認までを原則ひとまとめ） :contentReference[oaicite:3]{index=3}
- **同じファイルを複数スレッドで同時に編集しない**（競合・破綻防止） :contentReference[oaicite:4]{index=4}
- 大きな変更は「スレッド分割」ではなく「worktree分割」を優先（後述）

### 3.2 Cancel wins（プロジェクト原則）

- 本プロジェクトのコア仕様：**CancelはResumeより強い（Cancel勝ち）**
- Codexへの指示・レビューでも、この原則を最優先とする（実装/テスト/ドキュメントすべて）

## 4. ブランチ/Worktree ルール

### 4.1 作業単位

- 1機能/1修正＝1ブランチ
- そのブランチに対応して **1 worktree**（Codex Appのworktree機能、またはgit worktree）を使う :contentReference[oaicite:5]{index=5}

### 4.2 命名

- `feat/<topic>` / `fix/<topic>` / `docs/<topic>` / `chore/<topic>`
- 例：`feat/cancel-wins-reducer` / `docs/core-api-contract`

## 5. 変更ポリシー（安全・品質）

### 5.1 変更は「小さく、検証可能に」

- 1コミットで「仕様追加＋巨大改修」を混ぜない
- 変更のたびに `lint` / `test` / `typecheck` の少なくとも1つを回す（プロジェクトで採用しているもの）

### 5.2 コマンド実行

- Codexはディレクトリ内でコマンド実行できるため、タスクに「実行して検証」まで含める :contentReference[oaicite:6]{index=6}
- 例：`npm test` / `npm run build` / `docker compose up --build`（採用しているコマンドに合わせる）

## 6. “仕様→実装”の成果物規約

Codexに依頼する作業は、必ず以下の成果物を生成すること（該当するもの）：

- 仕様更新：`docs/*.md`（差分が追える形）
- 実装：`services/*`（該当モジュール）
- テスト：`*.test.*` など（プロジェクト規約に従う）
- 変更要約：PR本文（要点、影響範囲、動作確認手順）

## 7. プロンプトの書き方（必須テンプレ）

Codexへ渡す依頼は、最低限以下を含める：

1) 目的（何を達成したいか）
2) 変更範囲（触って良い/ダメなファイル）
3) 受け入れ条件（テスト、期待挙動、成功判定）
4) 既知の原則（Cancel wins 等）

## 8. Custom Prompts / Skills の扱い

- **Custom Prompts**：ローカル（例：`~/.codex`）に置かれ、リポジトリ共有されない :contentReference[oaicite:7]{index=7}  
- 共有したい運用・知識は **Skills** としてリポジトリに置く（本プロジェクトは原則 Skills 推奨） :contentReference[oaicite:8]{index=8}

## 9. 例：Codexに出す依頼の型（コア実装）

- 「Cancel wins を破らない」
- 「409/422/冪等性の規約を守る」
- 「同時リクエスト耐性（FOR UPDATE / version）」

上記を受け入れ条件に含め、変更点とテスト手順をPRに残す。
