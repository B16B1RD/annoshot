# 探索サマリ: macshot の Windows 版 (仮称 winshot)

日付: 2026-08-26 / セッション: /unknowns

## 出発点

macshot (https://macshot.io / https://github.com/sw33tLie/macshot、macOS 用スクショ・録画 OSS) の Windows 版を作れないか。動機は「自分が Windows で使いたい」。ShareX は未知だった。

## 確定したこと

### 目的 (これが最上位。手段はすべてここから導く)

**操作説明ビジュアル (注釈付き静止画 + ショート動画) の制作が、キャプチャ → 編集 → クリップボードまで一続きの体験でできる道具。**
macshot エディタのスクリーンショット 1 枚 (macshot.io の preview 画像) が要件定義そのもの: 機密の検閲・指定部分のクローズアップ・吹き出しや図形文字の簡単描画・ショート動画による操作紹介。

### 移植は不可、フルスクラッチ再実装

- macshot は Swift + AppKit、104 ファイル / 約 5.2 万行 (UI が 3.5 万行)
- ScreenCaptureKit / Vision / AVFoundation / Carbon など macOS 専用 API に全面依存。持ち越せるコードはゼロ
- GPLv3 なので仕様・検出パターン・UX の参考は自由。自分用で配布しなければ GPL 義務自体が発生しない

### v1 スコープ

| 領域 | 内容 |
|---|---|
| キャプチャ | ホットキー → フリーズ画面 → 領域/ウィンドウ選択 |
| 静止画編集 | 検閲 (手動ぼかし/ピクセル化 + **自動 PII・顔検出**)、円形ルーペ (クローズアップ)、吹き出し番号マーカー (自動連番 + ポインタコーン)、矢印/ペン/矩形/楕円/テキスト/絵文字スタンプ、Beautify (グラデ背景 + パディング + 角丸)、オブジェクト選択編集 + undo/redo |
| 録画 | 領域録画 → MP4/GIF |
| 動画編集 | トリム + **自動ズーム + クリック強調** + 動画内検閲 (macshot 動画エディタ同等) |
| 出口 | クリップボード + ローカル保存のみ。アップロード機能は作らない |

落とすもの: OCR 翻訳・スクロールキャプチャ・アップロード 3 系統・履歴パネル・Pin・40 言語対応・自動更新。規模感は 1〜2 万行級 (5 万行級から縮小)。

### 技術スタック: C# + Avalonia (推奨確定)

- 坂口さんは C# 未経験だが抵抗なし
- 根拠: Skia 描画がキャンバス編集向き / ShareX v20 の新エディタ (18 ツール + 232 効果) が同構成で実績 / WinRT API へのアクセスが素直
- Windows API マップ: キャプチャ・録画 = Windows.Graphics.Capture、OCR = Windows.Media.Ocr、顔検出 = Windows.Media.FaceAnalysis.FaceDetector (精度不足なら ONNX + YuNet)、グローバルホットキー = RegisterHotKey、動画書き出し = ffmpeg (or Media Foundation)

### 設計の核心 2 点

1. **クリックメタデータ方式**: 録画中にクリック座標 + タイムスタンプを記録し、編集時に自動ズーム候補を生成する (映像に焼き込まない)。Screen Studio / macshot `EffectsVideoCompositor` と同じ思想。「簡単な操作紹介」の品質はここで決まる
2. **PII 検閲エンジンは小さい**: macshot の `Services/AutoRedactor.swift` は 327 行。構成 = OCR で文字と座標取得 → 正規表現 13 パターン (メール/電話/SSN/カード 3 種/CVV/期限/IPv4/AWS キー/secret 代入/hex/Bearer) + 分割カード番号グルーピング等 2 パス + 顔・人物検出 API 各 1 呼び出し。同じ構成を Windows 標準 API で再現可能

## 却下した代替案

- **ShareX で済ます**: 機能比較の結果、自動 PII 検閲・録画後の動画エディタ・システム音声ワンタグルが欠落。前 2 つが必須要件のため不足 (逆にアップロード・自動化・付属ツールは ShareX 優位だが目的に不要)
- **ShareX + LosslessCut + 自作 redact CLI の寄せ集め** (ShareX Actions 連携): 最小コストだが「一続きの体験」という目的に反する。※この案をコスト起点で推奨して「最小コストで考える時点でダメ。目的ありき」との指摘を受けた経緯あり (memory: goal-first-not-cost-first)
- **macshot フル互換移植 (5 万行級)**: 目的に寄与しない機能が大半
- **Tauri / Electron**: Web が主戦場という強い理由がなく、体験品質・API アクセスで C# ネイティブ優位

## 未解決の問い

- **Avalonia で全画面フリーズオーバーレイ** (マルチモニタ・DPI 混在環境含む) が滑らかに作れるか → 技術 spike で最初に検証すべき No.1
- 動画リアルタイムプレビューの実装方式: Avalonia 上の自前フレーム合成 / libmpv 埋め込み / ffmpeg 逐次生成のどれか
- **日本語 PII パターン**: macshot のパターンは北米中心 (SSN 等)。日本の電話番号・マイナンバー等の自作が必要。Windows.Media.Ocr の日本語精度も未検証 (言語パック依存)
- Windows.Graphics.Capture の制約: カーソル込み/抜きの切替、フリーズ用静止フレーム取得、除外ウィンドウ
- px 計測ルーラーの要否 (未回答・優先度低)
- 録画のシステム音声要否 (未回答。操作紹介用途ならマイク/無音で足りる可能性が高い)
- プロジェクト名 (winshot は仮称、衝突未調査)・リポジトリ置き場所 (~/Projects/personal/ 新規想定)

## 発見した盲点

- **ShareX は 2026 年に大きく現代化していた**: v20.0.2 で Avalonia 製新エディタ、v21.0.0 で背景除去追加。「busy UI」の定評は旧世代の話 (今回の要件では依然不足だが、比較情報として)
- 動画トリム単体は **LosslessCut** で解決済みの領域。一体型完成までの繋ぎに使える
- ShareX の **Actions (外部 exe 連携)** でパイプライン拡張が可能 (却下案の部品だが知見として)
- PII 検閲は「大物機能」ではなく OCR + 正規表現 + 検出 API の小さな合成
- クリック強調は録画時焼き込みではなく「メタデータ + 後編集」が主流

## 成果物

- 本サマリのみ。プロトタイプは未作成
- 参照した macshot ソースはセッションの scratchpad に clone したもので、セッション終了で消える (必要なら再 clone: `git clone --depth 1 https://github.com/sw33tLie/macshot`)

## 次のステップ

1. **技術 spike** (実装前の最後の unknowns 潰し、使い捨てコード):
   - (a) C#/Avalonia + Windows.Graphics.Capture でフリーズ画面 + 領域選択オーバーレイ
   - (b) Windows.Media.Ocr + FaceDetector で画像 1 枚の自動 PII/顔検閲 PoC
   - この 2 つが通れば設計リスクの大半が消える
2. リポジトリ新設 → `/rite:issue-create` に本サマリを入力として渡し Issue 化 (規模的に XL 自動分解の対象)
3. 編集画面のレイアウトを先に固めたければ /unknowns のプロトタイプ (使い捨て HTML モック) を挟む選択肢もある
