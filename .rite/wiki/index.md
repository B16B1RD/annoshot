---
okf_version: "0.2"
description: "rite Experience Wiki — プロジェクト固有の経験則 bundle（OKF v0.2 準拠）"
---

# Wiki Index

このファイルは Wiki 全ページのカタログです。Ingest サイクルごとに `## ページ一覧` の 5 列テーブルが自動更新されます。

bundle-root の frontmatter で OKF（Open Knowledge Format）v0.2 への準拠を `okf_version: "0.2"` として宣言します（ただし**ページカタログの形式は OKF の箇条書き `* [title](path) - desc` から意図的に逸脱**し、下記の 5 列テーブルを使います。v0.2 §8 は v0.1 から不変）。各ページは `## ページ一覧` テーブルの 1 行として登録されます（列順: ページ / ドメイン / サマリー / 更新日 / 確信度）。各値のドメイン / 更新日 / 確信度はページ本体の frontmatter を Source of Truth とする写しです。サマリー列は frontmatter `description` があればその写し、無ければ index 側に蓄積された値そのものです（`description` は optional なので再生成できません）。ページ列のリンクテキストとサマリー列にはセル区切りのエスケープが適用されます。`## 統計` の 3 行は ingest が同期します（節を削除すると同期はスキップされ、総ページ数は `/rite:wiki-lint` のレポート出力で確認できます）。

## ページ一覧

| ページ | ドメイン | サマリー | 更新日 | 確信度 |
|--------|---------|---------|--------|--------|
| [vendor した生成テンプレートは消費者側で意味を持たないコメント参照を削除で解消する](pages/heuristics/vendored-template-comment-cleanup.md) | heuristics | 生成器のテンプレートをそのままコミットすると、削除済みマーカーや上流リポジトリ相対パスへのコメント参照が残り読者が追跡先を失う。設定値を変えず参照行だけ削除し、消費者側で意味を持つ情報のみ残すのが最小差分で、upgrade 差分ノイズとの天秤で判断する。 | 2026-09-02T10:30:00+00:00 | high |
| [.NET プロジェクトでは rite の commands 自動検出が効かないため csproj 作成後に明示設定する](pages/heuristics/dotnet-commands-explicit-config.md) | heuristics | rite の build/test/lint 自動検出は package.json や pyproject.toml 等を手がかりにするため .NET では null のままになり、テスト検証が skip され TDD が degraded になる。csproj 作成後に dotnet コマンドを rite-config.yml へ明示する。 | 2026-09-02T10:30:00+00:00 | medium |
| [.NET の GitHub Actions は global.json で SDK を固定し action は保守中のメジャーへ揃える](pages/heuristics/dotnet-ci-sdk-pin-and-action-maintenance.md) | heuristics | setup-dotnet の dotnet-version は下限指定にしかならず、global.json 不在だと runner の最上位 SDK が選ばれて LangVersion=latest が開発機とずれる。global.json + global-json-file で major 跨ぎを防ぎ、action のメジャータグは保守状況が action ごとに異なるため package-lock を照合して保守中の系列へ上げる。 | 2026-09-02T12:35:00+00:00 | high |
| [Windows ランナーの CRLF checkout と end_of_line=lf の衝突は .gitattributes で LF を固定して解消する](pages/heuristics/windows-runner-crlf-gitattributes-lf.md) | heuristics | windows-latest は core.autocrlf=true で CRLF に変換して checkout するため、.editorconfig の end_of_line=lf と衝突して dotnet format --verify-no-changes が全行 ENDOFLINE で失敗する。.gitattributes に text=auto eol=lf を置けば checkout 側で LF が固定され、format check と editorconfig が一致する。 | 2026-09-02T12:35:00+00:00 | high |
| [環境制約で Issue の MUST を計測できない spike は結論を暫定化し 4 箇所を同期する](pages/heuristics/spike-unmeasured-must-provisional-conclusion-sync.md) | heuristics | 使い捨て spike でも Issue の MUST を未計測のまま結論を確定すると複数 reviewer から仕様不整合 CRITICAL が並ぶ。不可逆・外向きの環境操作（表示スケール変更等）は自律実行せず、結論を暫定と明記して RESULT・PR 本文・元 Issue の Decision Log・follow-up Issue の 4 箇所を同期する。 | 2026-09-02T16:58:00+00:00 | high |
| [fix で文書の記述を一般化・述語化するたびに新しい不整合が生まれる](pages/anti-patterns/doc-fix-generalization-breeds-new-drift.md) | anti-patterns | レビュー指摘への fix で文書の契約文を限定付きから一般化したり、列挙をクラス述語（強調行など）に置き換えると、限定で真だった主張が偽になり次 cycle の指摘源になる。修正は列挙をそのまま残し述語を足さない最小差分にし、限定句は全出現箇所へ伝播させる。 | 2026-09-02T16:58:00+00:00 | high |
| [計測目的の Win32 P/Invoke と Avalonia 起動・終了は fail-fast と単一入口で組む](pages/patterns/measurement-spike-fail-fast-single-entry.md) | patterns | 計測 spike で P/Invoke 失敗を既定値（dpi=96 や座標 0,0）に潰すと症状が計測値に混入する。GetLastWin32Error を使う DllImport には SetLastError=true を付けて throw し、境界を跨ぐ矩形は入口で 1 度だけ交差させ、Avalonia の fire-and-forget な起点は Dispatcher.UIThread.Post で遅延し Shutdown は finally に置く。既定出力先は AppContext.BaseDirectory 配下に閉じる。 | 2026-09-02T16:58:00+00:00 | medium |

## 統計

- 総ページ数: 7
- ドメイン別: patterns=1, heuristics=5, anti-patterns=1
- 最終更新: 2026-09-02T16:58:00+00:00
