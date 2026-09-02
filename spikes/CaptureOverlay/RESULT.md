# 技術 spike (a): Windows.Graphics.Capture フリーズ画面 + 全画面オーバーレイ検証 — 結果

Issue #6 の成果物。「Avalonia で全画面フリーズオーバーレイ（マルチモニタ・DPI 混在含む）が滑らかに作れるか」と
Windows.Graphics.Capture（以下 WGC）の制約を実測し、Sub-4（キャプチャ本実装）の前提を確定する。
コード（`spikes/CaptureOverlay/`）は使い捨てで、再利用を前提にしない。

## 結論（Sub-4 で採る方式）— **暫定**

**Avalonia 方式を採る（暫定）。** オーバーレイは Avalonia の `Window`（`SystemDecorations=None` / `Topmost` / `ShowInTaskbar=false`）で作り、
フレーム取得は WGC（`Windows.Graphics.Capture` の WinRT 投影 + `IGraphicsCaptureItemInterop` / D3D11 デバイス作成の最小 P/Invoke）で行う。
Win32 レイヤードウィンドウ直叩きは不要。

**暫定である理由（AC-3 未達）**: Issue 4.4 MUST「DPI 100% と 150% の混在で実測しずれを px で記録する」は、計測環境がモニタ 1 台（150%）のため
**未実施**。Issue Assumptions の代替手段（表示スケールを 100% に変更して再計測）も本 PR では実施していない（実機の表示設定変更を伴うため
坂口さんの手で行う）。したがって AC-3 / AC-5 は未達で、本結論は「150% 単独では成立する」までを確認した暫定値である。
確定には下記「未計測」節 1. の追加計測が必要で、その結果で `size corrected` / `position corrected` の補正行が出た場合は
`EnsureGeometry` の補正方式を Sub-4 の実装指針に含める。なお Assumptions の代替手段（単独スケール 2 回計測）は AC-3 の Given
「100% と 150% が同時接続」を満たさないため、それだけで AC-3 達成とはせず、2 台構成での再計測または Issue 側での Given 緩和の判断を要する。

根拠（4K 1 台・150% での実測、詳細は後述）:

| 観点 | 結果 |
|---|---|
| 表示遅延 | トリガ → 全オーバーレイ初回描画 中央値 **99.8 ms**（目標 200 ms 以下を達成） |
| 座標一致 | 150% スケールで表示ずれ **0 px**、`PointToScreen` の差 **0 px** |
| Avalonia の DPI 追従 | `Opened` 時点で `RenderScaling=1.5` が Win32 の実効 DPI と一致し、サイズ / 位置の補正は不要だった |
| カーソル込み / 抜き | `IsCursorCaptureEnabled` で切替可（差分 165 px） |
| オーバーレイ自身の除外 | `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` で除外可（WGC / GDI とも） |
| 代替 API の要否 | 本環境では不要。GDI BitBlt は `--gdi` で残してあり、`IsSupported()==false` の環境向けフォールバック候補（**未サポート環境での GDI 経路の実測は本 spike では未実施**。`--gdi` は WGC 未サポート判定を迂回して GDI で続行する） |

## 4.3 の表

| 項目 | 内容 |
|------|------|
| 環境 | Windows 11 build 10.0.26200 (x64)、.NET 8.0.30、Avalonia 11.3.20。モニタ 1 台: `\\.\DISPLAY1` 3840x2160、144 dpi（150%）。**DPI 混在なし** |
| 静止フレーム取得時間 | WGC: 中央値 **56.5 ms**（最小 51.9 / 最大 139.1、10 回）。GDI BitBlt: 中央値 71.0 ms（最小 62.1 / 最大 106.2） |
| オーバーレイ表示遅延 | WGC 経路: 全モニタ取得 57.2 ms → 全オーバーレイ初回描画 **99.8 ms**（最小 90.8 / 最大 358.4、10 回中央値）。GDI 経路: 87.2 ms → 139.4 ms |
| 座標一致 | 150% モニタで検証矩形 (1792, 952, 256, 256) のずれ **dx=0, dy=0**、`PointToScreen` 差 (0, 0)。**DPI 混在（100% + 150% 同時）は未計測 = AC-3 未達**（下記「未計測」1.） |
| カーソル / 除外 | カーソル: `GraphicsCaptureSession.IsCursorCaptureEnabled`（切替可、差分 165 px）。除外: `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)`（除外前 8,293,248 px → 除外後 0 px）。`IsBorderRequired` は OS 側に存在（`ApiInformation` true）だが TFM 19041 の投影に無く未検証 |
| 結論 | **Avalonia（暫定）**（オーバーレイ = Avalonia Window、取得 = WGC + 最小 P/Invoke）。Win32 直叩きは不要。DPI 混在の追加計測で確定する |

## 環境

- 計測日時: 2026-09-03 00:05 (+09:00)
- OS: Microsoft Windows NT 10.0.26200.0 (x64)
- .NET 8.0.30 / Avalonia 11.3.20.0 / TFM `net8.0-windows10.0.19041.0`
- モニタ: `\\.\DISPLAY1` 3840x2160 @ (0,0)、144 dpi（150%）、primary。1 台のみ
- `GraphicsCaptureSession.IsSupported()` == true
- 実行方法: WSL2 でビルドした `bin/Release` 出力を Windows 側 `%TEMP%` にコピーし、Windows の `dotnet.exe`（SDK 8.0.424）で `--auto` を実行

## 計測詳細

### AC-1: 静止フレーム取得（10 回、モニタごと）

| 経路 | 取得 px | 中央値 ms | 最小 ms | 最大 ms | 解像度一致 |
|---|---|---|---|---|---|
| WGC (`SoftwareBitmap.CreateCopyFromSurfaceAsync` で CPU 読み出し) | 3840x2160 | 56.5 | 51.9 | 139.1 | OK |
| GDI BitBlt (`CAPTUREBLT`) | 3840x2160 | 71.0 | 62.1 | 106.2 | OK |

- WGC の計測はセッション作成（item / frame pool / session）から最初の `FrameArrived` を受けて CPU に読み出すまでを含む。
- 4K 1 枚で 50〜60 ms のうち、CPU 読み出し（GPU → `SoftwareBitmap` → `byte[]`、33 MB）が支配的と推定。Sub-4 で複数モニタを並列取得する場合はこの部分の並列化が効く。
- 最大値（139.1 ms）は 1 回目（JIT / 初回 D3D 初期化）。

### AC-2: オーバーレイ表示遅延（10 回）

| 経路 | 全モニタ取得完了まで（中央値） | トリガ → 全オーバーレイ初回描画（中央値） | 最小 / 最大 |
|---|---|---|---|
| WGC | 57.2 ms | **99.8 ms** | 90.8 / 358.4 |
| GDI | 87.2 ms | 139.4 ms | 127.9 / 505.9 |

- 「初回描画」は `Window.Show()` 後、`RequestAnimationFrame` が 2 回呼ばれた時点。
- 取得を除いたオーバーレイ自体のコスト（Window 生成 + `WriteableBitmap` 作成 + 初回描画）は約 40〜50 ms。
- 最大値は 1 回目（Avalonia の初回ウィンドウ生成 / シェーダ初期化）。2 回目以降は 100 ms 前後で安定。
- 目標 200 ms 以下: **達成**。タスクバーは topmost の全面ウィンドウで隠れる（`ShowInTaskbar=false`）。

### AC-3: 座標一致

判定基準: 「フレームから切り出した矩形」と「オーバーレイ表示中に同じ物理矩形を BitBlt したもの」の最小差分オフセット（±8 px 探索）が |dx|,|dy| ≤ 1 px、かつ `PointToScreen` の結果が Win32 の期待座標と ≤ 1 px。
`Alignment` のセルフチェック（既知オフセット (0,0) / (3,-2) / (-7,5) / (8,8) の復元）は 4 ケースとも OK。

| モニタ | DPI | scale | 検証矩形 (screen px) | ずれ (dx, dy) | PointToScreen 差 (px) | 判定 |
|---|---|---|---|---|---|---|
| `\\.\DISPLAY1` | 144 | 1.5 | 1792, 952, 256, 256 | 0, 0（diff 0.00） | (0, 0) | 一致 |

- Avalonia の配置ログ: `[Opened] RenderScaling=1.5 (Win32 1.5) ClientSize=2560x1440 Position=0,0 AvaloniaScreen=0,0,3840,2160 scaling=1.5`。
  表示前に Win32 の実効 DPI から論理サイズ（物理 px / scale）を決めて `Position` を物理 px で置く方式で、表示後の補正は発生しなかった。
- **DPI 混在（100% + 150% の同時接続、または表示スケール変更による再現）は本環境（モニタ 1 台）では未計測。** 下記「未計測」を参照。

### AC-4: カーソルと除外

- カーソル込み / 抜き: `IsCursorCaptureEnabled`（`ApiInformation.IsPropertyPresent` == true）を true / false にして 2 枚取得、差分ピクセル数 165 → **切替可**。
- オーバーレイ自身の除外: オーバーレイ全面をマゼンタ（#FF00FF）で塗り、`SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)` の前後で WGC 取得。
  センチネル画素数 8,293,248 → 0 で **除外可**。GDI BitBlt 経路でも同様に 0（除外はどちらの取得経路にも効く）。
- 補足: 本方式は「取得してから表示する」ため、静止画では除外は必須ではない。連続フレーム（Sub-8）で自身を映さないための手段として有効。
- `IsBorderRequired`（黄枠抑止）: OS 26200 には存在するが TFM 19041 の投影に無いため未検証。API の導入ビルドは **10.0.20348.0**（Windows 10 version 2104、
  `Windows.Foundation.UniversalApiContract` v12.0）で、Sub-4 で TFM を `net8.0-windows10.0.20348.0` 以上に上げれば型として使える（採用予定 SDK が 22621 なら
  それでもよいが、必要最小は 20348）。黄枠を消すには `GraphicsCaptureAccess.RequestAccessAsync(GraphicsCaptureAccessKind.Borderless)` でユーザー同意を得る
  ことが**必要**で、その呼び出しにはパッケージマニフェストの `graphicsCaptureWithoutBorder` capability 宣言が要る（未パッケージアプリでは capability を
  宣言できないため、黄枠抑止はパッケージ化が前提になる）。
  出典: https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.graphicscapturesession.isborderrequired

### AC-6: 未サポート環境

- `CaptureSupport.Probe` は `OperatingSystem.IsWindows()` / ビルド 18362 以上 / `GraphicsCaptureSession.IsSupported()` の順に判定し、Windows.* 型の参照は最後の 1 メソッドに閉じている。
- WSL2（Linux）で実行: `Windows.Graphics.Capture は未サポートです（理由: Windows ではありません ...）` を出力し終了コード 0、Avalonia は起動しない。
- Windows で `--force-unsupported`: `IsSupported()` の戻り値だけを false に差し替え、同じ分岐で `（理由: --force-unsupported 指定）` を出力し終了コード 0。
- 未サポート時の代替は GDI BitBlt（`--gdi`、本 spike に実装済み、カーソル切替不可）または Desktop Duplication。

## 未計測（坂口さんの実機で追加確認が必要な項目）

1. **DPI 混在での座標一致（AC-3 の核心、未達）**: 計測環境はモニタ 1 台のため混在を再現できなかった。Issue の前提どおり、
   (a) 2 台目を接続して 100% + 150% にする、または (b) 表示スケールを 100% に変更してもう 1 回 `--auto` を実行し、
   `out/measurements.md` の「座標一致」表と「ウィンドウ配置ログ」（`size corrected` / `position corrected` 行の有無）を本ファイルに転記する。
   混在時に補正行が出た場合は、`OverlayWindow.EnsureGeometry` の方式（Win32 実効 DPI で初期配置 → `RenderScaling` で補正）が Sub-4 の実装指針になる。
2. **手動ドラッグ（T-03）**: 既定モードで 150% モニタ上の UI 要素を囲んで `out/crop-N.png` を目視確認する。`--auto` の自動ずれ算出（0 px）と整合するはず。
3. **WGC の黄枠**: セッションが ~50 ms しか生きないため目視で見えるかは未確認。

## 検証手順

本 spike は `Annoshot.sln` に含めず **CI（`.github/workflows/ci.yml`）の対象外**。代替として以下を手元で実行する。

```sh
# ビルド（WSL2 / Windows どちらでも可。ルートの Directory.Build.props が警告=エラーで効く）
dotnet build spikes/CaptureOverlay -c Release

# フォーマット検証
dotnet format spikes/CaptureOverlay/CaptureOverlay.csproj --verify-no-changes

# 未サポート経路（AC-6 / T-06）: WSL でもそのまま動く。終了コード 0 とメッセージを確認
dotnet run --project spikes/CaptureOverlay -c Release -- --force-unsupported

# 全自動計測（Windows のみ）。全モニタが数秒間オーバーレイで覆われる。結果は out/measurements.md と PNG
dotnet run --project spikes/CaptureOverlay -c Release -- --auto

# GDI BitBlt 経路との比較
dotnet run --project spikes/CaptureOverlay -c Release -- --auto --gdi

# 手動: オーバーレイ上で矩形ドラッグ → out/crop-N.png に保存、ずれを HUD に表示。Esc で終了
dotnet run --project spikes/CaptureOverlay -c Release
```

WSL2 から Windows で実行する場合は、`bin/Release/net8.0-windows10.0.19041.0/` を Windows 側のパス（例: `%TEMP%\CaptureOverlay-spike`）にコピーし、
そのディレクトリで `dotnet.exe CaptureOverlay.dll --auto` を実行する（UNC パス上では `cmd.exe` が動かないため）。

**出力先と機微データの扱い**: 出力（`measurements.md` と PNG）は既定で実行ファイルと同じ場所の `out/`（= `bin/Release/.../out/`、`.gitignore` の `bin/` 配下）に
書かれる。`--out` で別の場所を指定した場合はその場所が git 管理外であることを確認すること。PNG は**デスクトップ全面のキャプチャ**（他アプリの内容・認証画面等が
写り込む）なので、確認後は削除し、リポジトリにコミットしない。`%TEMP%\CaptureOverlay-spike` にコピーして実行した場合も、終了後にそのディレクトリを削除する。
`--auto` はエラー終了時に終了コード 1 を返すので、無人実行では終了コードで成否を判定できる。

## Sub-4 への申し送り

- オーバーレイ: Avalonia `Window` をモニタごとに 1 枚。`Position` は物理 px、`Width/Height` は「物理 px / 実効 DPI スケール」で置き、`Opened` / `ScalingChanged` で `RenderScaling` と突き合わせて補正する（本 spike の `OverlayWindow.EnsureGeometry`）。
- 座標変換: ウィンドウ論理座標 → 物理 px は Avalonia の `PointToScreen` をそのまま使えばよい（150% で 0 px ずれ）。
- 取得: WGC の CPU 読み出しは `SoftwareBitmap.CreateCopyFromSurfaceAsync` で外部ライブラリ無しに済む。複数モニタは並列取得で 50 ms 台に収める余地あり。
- 自身の除外: `WDA_EXCLUDEFROMCAPTURE` が WGC / GDI 双方に効く。録画（Sub-8）で使う。
- TFM: `IsBorderRequired` を使うなら `net8.0-windows10.0.20348.0` 以上へ（API 導入ビルド）。黄枠抑止には `RequestAccessAsync(Borderless)` +
  `graphicsCaptureWithoutBorder` capability（パッケージ化）が必要。19041 のまま / 未パッケージなら黄枠は受容する。
- フォールバック: `IsSupported()==false` の環境では GDI BitBlt（カーソル無し、本環境では遅延 +40 ms 程度）が候補。ただし未サポート環境での
  GDI 経路は本 spike では未実測（WGC サポート機で `--gdi` を走らせた比較値のみ）。
