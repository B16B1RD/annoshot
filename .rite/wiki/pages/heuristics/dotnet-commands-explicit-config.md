---
type: "heuristics"
title: ".NET プロジェクトでは rite の commands 自動検出が効かないため csproj 作成後に明示設定する"
domain: "heuristics"
description: "rite の build/test/lint 自動検出は package.json や pyproject.toml 等を手がかりにするため .NET では null のままになり、テスト検証が skip され TDD が degraded になる。csproj 作成後に dotnet コマンドを rite-config.yml へ明示する。"
created: "2026-09-02T10:30:00+00:00"
generated: { by: "rite-wiki-ingest/claude-fable-5-1", at: "2026-09-02T10:30:00+00:00" }
sources:
  - type: "reviews"
    resource: "raw/reviews/20260902T101137Z-pr-2.md"
tags: []
confidence: medium
---

# .NET プロジェクトでは rite の commands 自動検出が効かないため csproj 作成後に明示設定する

## 概要

rite の build/test/lint 自動検出は package.json や pyproject.toml 等を手がかりにするため .NET では null のままになり、テスト検証が skip され TDD が degraded になる。csproj 作成後に dotnet コマンドを rite-config.yml へ明示する。

## 詳細

- 観測: rite-config.yml の `commands.build` / `commands.test` / `commands.lint` は `null = auto-detect` と注記されているが、自動検出の対象は Node.js（package.json）、Python（pyproject.toml）、Rust（Cargo.toml）、Go（go.mod）、Makefile であり、.NET（csproj / sln）は含まれない。
- 帰結: `verification.run_tests_before_pr: true` はテストコマンド未設定として skip され、`tdd.enabled: true` は Red/Green 自動実行のない degraded モードになる。いずれも理由付きで表示されるため silent failure ではないが、品質ゲートが実質無効になる。
- 対処: csproj / sln を作成した時点で `dotnet build` / `dotnet test` / `dotnet format --verify-no-changes` 等を rite-config.yml に明示設定する。csproj が存在しない段階で設定すると逆に失敗するため、scaffold と同時に行う。
- 併せて: CI ワークフローが未整備なら、実装が入る前に dotnet build / test を走らせる workflow を用意すると commands 設定に依存せず回帰を検出できる。

## 関連ページ

- [vendor した生成テンプレートは消費者側で意味を持たないコメント参照を削除で解消する](./vendored-template-comment-cleanup.md)

## ソース

- [PR #2 review results](../../raw/reviews/20260902T101137Z-pr-2.md)
