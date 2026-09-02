---
type: "anti-patterns"
title: "fix で文書の記述を一般化・述語化するたびに新しい不整合が生まれる"
domain: "anti-patterns"
description: "レビュー指摘への fix で文書の契約文を限定付きから一般化したり、列挙をクラス述語（強調行など）に置き換えると、限定で真だった主張が偽になり次 cycle の指摘源になる。修正は列挙をそのまま残し述語を足さない最小差分にし、限定句は全出現箇所へ伝播させる。"
created: "2026-09-02T16:58:00+00:00"
generated: { by: "rite-wiki-ingest/claude-fable-5-1", at: "2026-09-02T16:58:00+00:00" }
sources:
  - type: "reviews"
    resource: "raw/reviews/20260902T160809Z-pr-16.md"
  - type: "reviews"
    resource: "raw/reviews/20260902T163013Z-pr-16.md"
  - type: "fixes"
    resource: "raw/fixes/20260902T163335Z-pr-16.md"
  - type: "reviews"
    resource: "raw/reviews/20260902T164714Z-pr-16.md"
  - type: "fixes"
    resource: "raw/fixes/20260902T165344Z-pr-16.md"
tags: []
confidence: high
---

# fix で文書の記述を一般化・述語化するたびに新しい不整合が生まれる

## 詳細の前提

4 cycle のレビューで blocking 指摘が 7 → 4 → 1 → 0 と収束したが、cycle 2 以降の指摘はすべて前 cycle の fix diff 自身が導入した文書の不整合だった。

## 概要

レビュー指摘への fix で文書の契約文を限定付きから一般化したり、列挙をクラス述語（強調行など）に置き換えると、限定で真だった主張が偽になり次 cycle の指摘源になる。修正は列挙をそのまま残し述語を足さない最小差分にし、限定句は全出現箇所へ伝播させる。

## 詳細

- 一般化の罠: 「--auto 限定で終了コードで成否判定できる」を「終了コードで成否判定できる」へ広げた結果、引数エラーの exit 2 など別経路を取りこぼした。契約文を広げるときは全経路（引数エラー / 計測失敗 / 未サポート正常終了 / runtime abort）を列挙し直す。
- 述語化の罠: 「確認すべきマーカー」の個別列挙を「`**` 強調行と NG が無いこと」という述語に置き換えたところ、`**` の付かない判定値（計測不能・カーソル非対応）を取りこぼし、逆に単一 DPI 環境で常に出る強調行を失敗と読ませる誤検知を生んだ。次 cycle の解消策は述語を削って列挙をチェックリストに戻し、例外 2 点だけ注記する simplification-first だった。
- 限定句の伝播漏れ: 「Windows 限定」の但し書きを本文に足しても、要約表のセルが取り残される。同じクラスの記述は文書内の全出現箇所へ伝播させる。
- 経路依存の確認項目（D3D11 ドライバ行は WGC 経路でしか出力されない）は、該当経路に限定して書く。正規手順として別経路（--gdi）も案内しているなら特に注意。
- 見分け方: 指摘の description が「前 cycle で導入した」「新設した」と名指ししているなら、追加パッチではなく当該機構ごと削除できないかを先に検討する。

## 関連ページ

- [環境制約で Issue の MUST を計測できない spike は結論を暫定化し 4 箇所を同期する](../heuristics/spike-unmeasured-must-provisional-conclusion-sync.md)

## ソース

- [PR #16 review results (cycle 2)](../../raw/reviews/20260902T160809Z-pr-16.md)
- [PR #16 review results (cycle 3)](../../raw/reviews/20260902T163013Z-pr-16.md)
- [PR #16 fix results (cycle 3)](../../raw/fixes/20260902T163335Z-pr-16.md)
- [PR #16 review results (cycle 4)](../../raw/reviews/20260902T164714Z-pr-16.md)
- [PR #16 fix results (nb-sweep)](../../raw/fixes/20260902T165344Z-pr-16.md)
