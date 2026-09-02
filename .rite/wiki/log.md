# Directory Update Log

このファイルは Wiki の変更履歴を OKF 予約ファイル構造（`## YYYY-MM-DD` 見出し + 散文 bullet、新しい順。v0.2 §9 は v0.1 から不変）で記録します（append-only、人間向け）。

skip 等の機械可読状態は **各 raw source の frontmatter（`ingest_status`）が Source of Truth** であり、本ログには保持しません（本ログは人間向けの変更履歴に純化しています）。

## 2026-09-02

* **init** — Wiki を初期化しました
* **Create**: [vendor した生成テンプレートは消費者側で意味を持たないコメント参照を削除で解消する](pages/heuristics/vendored-template-comment-cleanup.md) — raw/reviews/20260902T101137Z-pr-2.md, raw/fixes/20260902T101618Z-pr-2.md を新規ページ化
* **Create**: [.NET プロジェクトでは rite の commands 自動検出が効かないため csproj 作成後に明示設定する](pages/heuristics/dotnet-commands-explicit-config.md) — raw/reviews/20260902T101137Z-pr-2.md を新規ページ化
* **lint:clean** — contradictions=0, stale=0, orphans=0, missing_concept=0, unregistered_raw=0, broken_refs=0
