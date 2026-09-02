---
type: "patterns"
title: "計測目的の Win32 P/Invoke と Avalonia 起動・終了は fail-fast と単一入口で組む"
domain: "patterns"
description: "計測 spike で P/Invoke 失敗を既定値（dpi=96 や座標 0,0）に潰すと症状が計測値に混入する。GetLastWin32Error を使う DllImport には SetLastError=true を付けて throw し、境界を跨ぐ矩形は入口で 1 度だけ交差させ、Avalonia の fire-and-forget な起点は Dispatcher.UIThread.Post で遅延し Shutdown は finally に置く。既定出力先は AppContext.BaseDirectory 配下に閉じる。"
created: "2026-09-02T16:58:00+00:00"
generated: { by: "rite-wiki-ingest/claude-fable-5-1", at: "2026-09-02T16:58:00+00:00" }
sources:
  - type: "reviews"
    resource: "raw/reviews/20260902T153743Z-pr-16.md"
  - type: "fixes"
    resource: "raw/fixes/20260902T154855Z-pr-16.md"
  - type: "reviews"
    resource: "raw/reviews/20260902T160809Z-pr-16.md"
  - type: "fixes"
    resource: "raw/fixes/20260902T161459Z-pr-16.md"
tags: []
confidence: medium
---

# 計測目的の Win32 P/Invoke と Avalonia 起動・終了は fail-fast と単一入口で組む

## 概要

計測 spike で P/Invoke 失敗を既定値（dpi=96 や座標 0,0）に潰すと症状が計測値に混入する。GetLastWin32Error を使う DllImport には SetLastError=true を付けて throw し、境界を跨ぐ矩形は入口で 1 度だけ交差させ、Avalonia の fire-and-forget な起点は Dispatcher.UIThread.Post で遅延し Shutdown は finally に置く。既定出力先は AppContext.BaseDirectory 配下に閉じる。

## 詳細

- fail-fast: GetMonitorInfoW / GetDpiForMonitor / GetCursorPos の失敗を既定値で握りつぶすと、ずれ計測やカーソル位置の数値に「失敗」が混ざり原因が追えない。計測コードでは throw に置き換える。`Marshal.GetLastWin32Error()` を診断に使うなら `DllImport(SetLastError = true)` を同時に付ける。片方だけだと診断値が無意味になる。
- 矩形の単一入口: 無言でクランプする Crop と、クランプしない BitBlt に同じ矩形を渡すとサイズ不一致例外になる。選択矩形はモニタ矩形と入口で 1 度だけ交差させ、全消費者に同じ矩形を渡す。
- Avalonia の起動と終了: `ShutdownMode.OnExplicitShutdown` で fire-and-forget な async 起点を持つ場合、初期化の同期失敗で即 Shutdown するとメインループ開始前になり「Dispatcher shut down」で落ちて exit code が壊れる（実機で exit 82）。起点を `Dispatcher.UIThread.Post` で遅延させると exit 1 が正しく返る。退路（Finish）内の IO 例外で Shutdown に到達しないとプロセスが残るので、Shutdown は finally に置く。無効計測（セルフチェック失敗）も終了コードに伝播させる。
- 未サポート分岐: 「未サポート時は --gdi を検討」と案内するなら、分岐条件はその同じオプションを見る。分岐をフラグで広げるときは未サポートの理由（非 Windows / API 不在）を区別し、プラットフォーム非対応は理由を問わず終了する。
- 出力先: 無人実行 CLI の既定出力を CWD 相対にすると、文書化した手順がリポジトリルートにデスクトップ全面 PNG のような機微データを落とす。既定は `AppContext.BaseDirectory` 配下（bin/ 以下）に閉じ、`.gitignore` と手順の両方で守る。

## 関連ページ

- [環境制約で Issue の MUST を計測できない spike は結論を暫定化し 4 箇所を同期する](../heuristics/spike-unmeasured-must-provisional-conclusion-sync.md)

## ソース

- [PR #16 review results](../../raw/reviews/20260902T153743Z-pr-16.md)
- [PR #16 fix results](../../raw/fixes/20260902T154855Z-pr-16.md)
- [PR #16 review results (cycle 2)](../../raw/reviews/20260902T160809Z-pr-16.md)
- [PR #16 fix results (cycle 2)](../../raw/fixes/20260902T161459Z-pr-16.md)
