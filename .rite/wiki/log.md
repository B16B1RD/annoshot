# Directory Update Log

このファイルは Wiki の変更履歴を OKF 予約ファイル構造（`## YYYY-MM-DD` 見出し + 散文 bullet、新しい順。v0.2 §9 は v0.1 から不変）で記録します（append-only、人間向け）。

skip 等の機械可読状態は **各 raw source の frontmatter（`ingest_status`）が Source of Truth** であり、本ログには保持しません（本ログは人間向けの変更履歴に純化しています）。

## 2026-09-02

* **init** — Wiki を初期化しました
* **Create**: [vendor した生成テンプレートは消費者側で意味を持たないコメント参照を削除で解消する](pages/heuristics/vendored-template-comment-cleanup.md) — raw/reviews/20260902T101137Z-pr-2.md, raw/fixes/20260902T101618Z-pr-2.md を新規ページ化
* **Create**: [.NET プロジェクトでは rite の commands 自動検出が効かないため csproj 作成後に明示設定する](pages/heuristics/dotnet-commands-explicit-config.md) — raw/reviews/20260902T101137Z-pr-2.md を新規ページ化
* **lint:clean** — contradictions=0, stale=0, orphans=0, missing_concept=0, unregistered_raw=0, broken_refs=0
* **Create**: [.NET の GitHub Actions は global.json で SDK を固定し action は保守中のメジャーへ揃える](pages/heuristics/dotnet-ci-sdk-pin-and-action-maintenance.md) — raw/reviews/20260902T120848Z-pr-14.md, raw/fixes/20260902T121130Z-pr-14.md, raw/reviews/20260902T122526Z-pr-14.md を新規ページ化
* **Create**: [Windows ランナーの CRLF checkout と end_of_line=lf の衝突は .gitattributes で LF を固定して解消する](pages/heuristics/windows-runner-crlf-gitattributes-lf.md) — raw/reviews/20260902T120848Z-pr-14.md を新規ページ化
* **lint:clean** — contradictions=0, stale=0, orphans=0, missing_concept=0, unregistered_raw=0, broken_refs=0
* **Create**: [環境制約で Issue の MUST を計測できない spike は結論を暫定化し 4 箇所を同期する](pages/heuristics/spike-unmeasured-must-provisional-conclusion-sync.md) — raw/reviews/20260902T153743Z-pr-16.md, raw/fixes/20260902T154855Z-pr-16.md, raw/reviews/20260902T160809Z-pr-16.md, raw/fixes/20260902T161459Z-pr-16.md, raw/reviews/20260902T164714Z-pr-16.md を新規ページ化
* **Create**: [fix で文書の記述を一般化・述語化するたびに新しい不整合が生まれる](pages/anti-patterns/doc-fix-generalization-breeds-new-drift.md) — raw/reviews/20260902T160809Z-pr-16.md, raw/reviews/20260902T163013Z-pr-16.md, raw/fixes/20260902T163335Z-pr-16.md, raw/reviews/20260902T164714Z-pr-16.md, raw/fixes/20260902T165344Z-pr-16.md を新規ページ化
* **Create**: [計測目的の Win32 P/Invoke と Avalonia 起動・終了は fail-fast と単一入口で組む](pages/patterns/measurement-spike-fail-fast-single-entry.md) — raw/reviews/20260902T153743Z-pr-16.md, raw/fixes/20260902T154855Z-pr-16.md, raw/reviews/20260902T160809Z-pr-16.md, raw/fixes/20260902T161459Z-pr-16.md を新規ページ化
* **lint:clean** — contradictions=0, stale=0, orphans=0, missing_concept=0, unregistered_raw=0, broken_refs=0
