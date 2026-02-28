## 🤖 マルチエージェント【確定版・動作確認済み 2026-02-28】

**kin で3エージェント並列実行を確認済み。必ずこのパターンを使うこと。**

CLAUDE_BIN=/home/muraocbot/.npm-global/bin/claude

並列起動パターン（env -u CLAUDECODE が必須）:
  env -u CLAUDECODE $CLAUDE_BIN --dangerously-skip-permissions -p 'タスク1' > /tmp/agent1.log 2>&1 &
  PID1=$!
  env -u CLAUDECODE $CLAUDE_BIN --dangerously-skip-permissions -p 'タスク2' > /tmp/agent2.log 2>&1 &
  PID2=$!
  env -u CLAUDECODE $CLAUDE_BIN --dangerously-skip-permissions -p 'タスク3' > /tmp/agent3.log 2>&1 &
  PID3=$!
  wait $PID1 $PID2 $PID3
  cat /tmp/agent1.log /tmp/agent2.log /tmp/agent3.log

なぜenv -u CLAUDECODEが必要か:
Claude CodeはCLAUDECODE環境変数をセットする。子プロセスに残るとネスト起動が拒否される。
env -u CLAUDECODEでその変数を除去してから起動することで解決。

タスク分解パターン:
- agent1: フロントエンド（UIコンポーネント・ページ）
- agent2: バックエンド（API・DB・ビジネスロジック）
- agent3: テスト（ユニット・E2E）
→ wait → 統合 → commit

ルール:
1. 複数ファイルにまたがる実装は必ず並列起動
2. 単独実行は1ファイル以内の小修正のみ
3. プロンプトは具体的に（ファイルパス・実装内容・コードスタイルを含める）
4. 完了報告に「何エージェントで並列実行したか」を明記

---

# CLAUDE.md
