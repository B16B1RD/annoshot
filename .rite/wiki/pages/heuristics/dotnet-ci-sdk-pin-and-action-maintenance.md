---
type: "heuristics"
title: ".NET の GitHub Actions は global.json で SDK を固定し action は保守中のメジャーへ揃える"
domain: "heuristics"
description: "setup-dotnet の dotnet-version は下限指定にしかならず、global.json 不在だと runner の最上位 SDK が選ばれて LangVersion=latest が開発機とずれる。global.json + global-json-file で major 跨ぎを防ぎ、action のメジャータグは保守状況が action ごとに異なるため package-lock を照合して保守中の系列へ上げる。"
created: "2026-09-02T12:35:00+00:00"
generated: { by: "rite-wiki-ingest/claude-fable-5-1", at: "2026-09-02T12:35:00+00:00" }
sources:
  - type: "reviews"
    resource: "raw/reviews/20260902T120848Z-pr-14.md"
  - type: "fixes"
    resource: "raw/fixes/20260902T121130Z-pr-14.md"
  - type: "reviews"
    resource: "raw/reviews/20260902T122526Z-pr-14.md"
tags: []
confidence: high
---

# .NET の GitHub Actions は global.json で SDK を固定し action は保守中のメジャーへ揃える

## 概要

setup-dotnet の dotnet-version は下限指定にしかならず、global.json 不在だと runner の最上位 SDK が選ばれて LangVersion=latest が開発機とずれる。global.json + global-json-file で major 跨ぎを防ぎ、action のメジャータグは保守状況が action ごとに異なるため package-lock を照合して保守中の系列へ上げる。

## 詳細

- 観測: `actions/setup-dotnet` に `dotnet-version: 8.0.x` だけを渡すと、runner image に同居する 9.x / 10.x SDK のうち最上位が MSBuild に選ばれる。`Directory.Build.props` の `LangVersion=latest` はコンパイラ同梱版に解決されるため、CI では C# 14 相当、README が要求する .NET 8 SDK だけの開発機では C# 12 になり、CI 緑のまま開発機でビルド不能になる経路が生じる。
- 対処: リポジトリルートに `global.json`（例: `sdk.version: 8.0.100`, `rollForward: latestFeature`, `allowPrerelease: false`）を置き、CI 側は `dotnet-version` ではなく `global-json-file: global.json` を指定する。`latestFeature` は同一 major.minor 内の feature band だけを float させるため major 跨ぎは防げ、開発機の feature band を縛らない。厳密固定が必要なら `latestPatch` + 実バージョンにする。
- 観測: メジャータグ `@v4` は action ごとに保守状況が異なる。`actions/setup-dotnet@v4` は v4.3.1 で更新が止まり form-data 2.5.1（既知脆弱性）を同梱したままだった一方、`actions/checkout@v4` は保守が続いていた。`gh api repos/<action>/contents/package-lock.json?ref=<tag>` で同梱依存を tag 直読すれば脆弱性の有無を実測できる。
- 対処: bump は両 action をまとめて保守中の最初のメジャー（本件では v5）へ上げる。Node 20 非推奨化は runner が Node 24 へ強制フォールバックするため単独では CI を止めないが、脆弱性修正は新しい系列にしか届かない。
- 副次: `rollForward` の緩さ、可変タグと SHA pin、undici 等の action 同梱依存の未修正 GHSA は、`permissions: contents: read` + secrets 不使用の構成では実害経路が無く、Decision Log に記録して secrets を扱う job を足す時点で再評価すればよい。

## 関連ページ

- [.NET プロジェクトでは rite の commands 自動検出が効かないため csproj 作成後に明示設定する](./dotnet-commands-explicit-config.md)
- [Windows ランナーの CRLF checkout と end_of_line=lf の衝突は .gitattributes で LF を固定して解消する](./windows-runner-crlf-gitattributes-lf.md)

## ソース

- [PR #14 review results](../../raw/reviews/20260902T120848Z-pr-14.md)
- [PR #14 fix results](../../raw/fixes/20260902T121130Z-pr-14.md)
- [PR #14 review results (cycle 2)](../../raw/reviews/20260902T122526Z-pr-14.md)
