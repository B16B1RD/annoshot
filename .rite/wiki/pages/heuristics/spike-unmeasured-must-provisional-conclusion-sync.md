---
type: "heuristics"
title: "環境制約で Issue の MUST を計測できない spike は結論を暫定化し 4 箇所を同期する"
domain: "heuristics"
description: "使い捨て spike でも Issue の MUST を未計測のまま結論を確定すると複数 reviewer から仕様不整合 CRITICAL が並ぶ。不可逆・外向きの環境操作（表示スケール変更等）は自律実行せず、結論を暫定と明記して RESULT・PR 本文・元 Issue の Decision Log・follow-up Issue の 4 箇所を同期する。"
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
  - type: "reviews"
    resource: "raw/reviews/20260902T164714Z-pr-16.md"
tags: []
confidence: high
---

# 環境制約で Issue の MUST を計測できない spike は結論を暫定化し 4 箇所を同期する

## 概要

使い捨て spike でも Issue の MUST を未計測のまま結論を確定すると複数 reviewer から仕様不整合 CRITICAL が並ぶ。不可逆・外向きの環境操作（表示スケール変更等）は自律実行せず、結論を暫定と明記して RESULT・PR 本文・元 Issue の Decision Log・follow-up Issue の 4 箇所を同期する。

## 詳細

- 観測: 技術 spike の受け入れ条件に「DPI 混在環境での実測」があったが、実機はモニタ 1 台・単一スケールで再現できなかった。計測できた範囲だけで結論を「成立」と書いたところ、security / application / tech-writer の 3 reviewer が独立に「Issue の MUST が未検証のまま結論確定」を CRITICAL として挙げた。
- 代替手段（表示スケールを一時的に変更して再計測）は実機設定を変える不可逆・外向きの操作なので、自律実行せずユーザーに委ねる判断が妥当。コード側では解決できないため、修正は文書側で「結論は暫定」「AC-x は未達」「未計測項目」を明示する形になる。
- 暫定化した結論は複数箇所に写しがある。RESULT.md の結論節、PR 本文の概要と未完了チェック項目、元 Issue の Decision Log、そして残作業を受ける follow-up Issue。片方だけ更新すると次 cycle で「同じ結論が 2 箇所で食い違う」指摘に変わるので、暫定化と同時に 4 箇所をまとめて更新する。
- follow-up Issue の Scope と RESULT.md の「残作業」の粒度を揃える。spike の残作業と後続 Sub-Issue の検証を混ぜると、どちらの Issue で何を確認するかが曖昧になる。

## 関連ページ

- [fix で文書の記述を一般化・述語化するたびに新しい不整合が生まれる](../anti-patterns/doc-fix-generalization-breeds-new-drift.md)
- [計測目的の Win32 P/Invoke と Avalonia 起動・終了は fail-fast と単一入口で組む](../patterns/measurement-spike-fail-fast-single-entry.md)

## ソース

- [PR #16 review results](../../raw/reviews/20260902T153743Z-pr-16.md)
- [PR #16 fix results](../../raw/fixes/20260902T154855Z-pr-16.md)
- [PR #16 review results (cycle 2)](../../raw/reviews/20260902T160809Z-pr-16.md)
- [PR #16 fix results (cycle 2)](../../raw/fixes/20260902T161459Z-pr-16.md)
- [PR #16 review results (cycle 4)](../../raw/reviews/20260902T164714Z-pr-16.md)
