---
type: "heuristics"
title: "Windows ランナーの CRLF checkout と end_of_line=lf の衝突は .gitattributes で LF を固定して解消する"
domain: "heuristics"
description: "windows-latest は core.autocrlf=true で CRLF に変換して checkout するため、.editorconfig の end_of_line=lf と衝突して dotnet format --verify-no-changes が全行 ENDOFLINE で失敗する。.gitattributes に text=auto eol=lf を置けば checkout 側で LF が固定され、format check と editorconfig が一致する。"
created: "2026-09-02T12:35:00+00:00"
generated: { by: "rite-wiki-ingest/claude-fable-5-1", at: "2026-09-02T12:35:00+00:00" }
sources:
  - type: "reviews"
    resource: "raw/reviews/20260902T120848Z-pr-14.md"
tags: []
confidence: high
---

# Windows ランナーの CRLF checkout と end_of_line=lf の衝突は .gitattributes で LF を固定して解消する

## 概要

windows-latest は core.autocrlf=true で CRLF に変換して checkout するため、.editorconfig の end_of_line=lf と衝突して dotnet format --verify-no-changes が全行 ENDOFLINE で失敗する。.gitattributes に text=auto eol=lf を置けば checkout 側で LF が固定され、format check と editorconfig が一致する。

## 詳細

- 観測: WSL / Linux では build・test・format がすべて通るのに、windows-latest 上の Format check だけが `error ENDOFLINE: Fix end of line marker. Replace 2 characters with '\n'` を全 .cs ファイルの全行で報告した。原因は runner の `core.autocrlf=true` による checkout 時の CRLF 変換で、`.editorconfig` の `end_of_line = lf` を `dotnet format` が強制するため衝突する。
- 対処: リポジトリルートに `.gitattributes` を追加し `* text=auto eol=lf` を宣言する（バイナリ拡張子は `binary` で除外）。checkout 側で LF が固定されるため `.editorconfig` の設定を緩める必要がない。
- 検出のコツ: Format check を Build / Test の後段に置くと書式逸脱だけで restore + build + test を全消費する。先行ジョブ化するか Build 直後へ移すと失敗が早く分かる。

## 関連ページ

- [.NET の GitHub Actions は global.json で SDK を固定し action は保守中のメジャーへ揃える](./dotnet-ci-sdk-pin-and-action-maintenance.md)

## ソース

- [PR #14 review results](../../raw/reviews/20260902T120848Z-pr-14.md)
