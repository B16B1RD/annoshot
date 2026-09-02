using System.Diagnostics;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CaptureOverlay.Capture;
using CaptureOverlay.Native;

namespace CaptureOverlay.Measure;

/// <summary>
/// spike の計測シナリオ。Avalonia UI スレッド上で async に進み、結果を <c>{out}/measurements.md</c> に書く。
/// --auto: 全計測を無操作で実行して終了。既定: オーバーレイを出し、ドラッグ切り出しと計測を Esc まで繰り返す。
/// </summary>
internal static class SpikeRunner
{
    // 「ずれ」判定: |dx|,|dy| がこの値以下なら一致（AC-3）
    private const int _shiftTolerancePx = 1;
    private const int _alignmentPatchSize = 256;
    private static readonly TimeSpan _renderSettle = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan _autoModeTimeout = TimeSpan.FromSeconds(120);

    public static async Task RunAsync(IClassicDesktopStyleApplicationLifetime desktop, SpikeOptions options)
    {
        var report = new Report(Path.Combine(options.OutDir, "measurements.md"));
        var session = new Session(desktop, options, report);
        try
        {
            await session.RunAsync();
        }
        catch (Exception ex)
        {
            // 無人実行の失敗は終了コードで検知できるようにする（measurements.md を開くまで気づけないのを防ぐ）
            report.Line($"**ERROR**: {ex}");
            Console.Error.WriteLine(ex);
            session.Finish(exitCode: 1);
        }
    }

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return double.NaN;
        }

        double[] sorted = values.Order().ToArray();
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    private static async Task SettleRenderAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Task.Delay(_renderSettle);
    }

    private sealed class Session
    {
        private readonly IClassicDesktopStyleApplicationLifetime _desktop;
        private readonly SpikeOptions _options;
        private readonly Report _report;
        private readonly List<OverlayWindow> _overlays = new();
        private IFrameSource? _source;
        private List<MonitorInfo> _monitors = new();
        private bool _measurementsValid = true;
        private int _cropCount;
        private bool _finished;

        public Session(IClassicDesktopStyleApplicationLifetime desktop, SpikeOptions options, Report report)
        {
            _desktop = desktop;
            _options = options;
            _report = report;
        }

        public async Task RunAsync()
        {
            if (_options.Auto)
            {
                // 無人実行で全画面 topmost が残り続けないよう、上限時間で強制終了する。
                // 初期化（出力先作成・モニタ列挙・D3D デバイス生成）の失敗経路も覆うため最初に登録する
                _ = Task.Delay(_autoModeTimeout).ContinueWith(
                    _ => Dispatcher.UIThread.Post(() =>
                    {
                        _report.Line($"**TIMEOUT**: {_autoModeTimeout.TotalSeconds:0} 秒で打ち切り");
                        Finish(exitCode: 1);
                    }),
                    TaskScheduler.Default);
            }

            Directory.CreateDirectory(_options.OutDir);
            _monitors = Win32.EnumerateMonitors();
            _source = _options.UseGdi ? new GdiCapture() : new WgcCapture();

            WriteEnvironment();

            if (_options.Auto)
            {
                bool selfCheckOk = Alignment.SelfCheck(out string detail);
                _measurementsValid = selfCheckOk;
                _report.Line(selfCheckOk
                    ? $"Alignment セルフチェック: OK ({detail})"
                    : $"**SELFCHECK FAILED** — 以降のずれ計測値は無効: {detail}");
            }

            await MeasureCaptureTimeAsync();
            FrameData[] frames = await MeasureOverlayAsync();

            if (_options.Auto)
            {
                await MeasureAlignmentAsync(frames);
                await MeasureCursorToggleAsync();
                await MeasureExclusionAsync();
                DumpGeometryLog();
                Finish();
                return;
            }

            _report.Section("手動ドラッグ結果");
            foreach (OverlayWindow overlay in _overlays)
            {
                overlay.RegionSelected += OnRegionSelected;
                overlay.SetHud($"{overlay.Monitor.Label}\nドラッグで切り出し / Esc で終了");
            }
        }

        /// <summary>Esc（両モード）。全画面 topmost が残らないよう、どの段階でも即終了できる退路。</summary>
        private void OnCancelled()
        {
            if (_finished)
            {
                return;
            }

            _report.Line("- Esc で中断");
            DumpGeometryLog();
            Finish();
        }

        public void Finish(int exitCode = 0)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            try
            {
                foreach (OverlayWindow overlay in _overlays)
                {
                    overlay.Close();
                }

                _overlays.Clear();
                _source?.Dispose();
                _report.Flush();
                Console.WriteLine($"結果: {_report.FilePath}");
            }
            catch (Exception ex)
            {
                // 退路自体の失敗（出力先に書けない等）でプロセスを残さない。Shutdown は finally で必ず通す
                Console.Error.WriteLine($"終了処理で例外: {ex}");
                exitCode = exitCode == 0 ? 1 : exitCode;
            }
            finally
            {
                _desktop.Shutdown(exitCode);
            }
        }

        private void WriteEnvironment()
        {
            _report.Section("環境");
            _report.Line($"- 計測日時: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
            _report.Line($"- OS: {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "x64" : "x86")})");
            _report.Line($"- .NET: {Environment.Version} / Avalonia: {typeof(Application).Assembly.GetName().Version}");
            _report.Line($"- 取得経路: {_source!.Name} (cursor toggle: {_source.SupportsCursorToggle})");
            if (_source is WgcCapture wgc)
            {
                _report.Line(wgc.D3DDriver == "HARDWARE"
                    ? "- D3D11 ドライバ: HARDWARE"
                    : $"- D3D11 ドライバ: {wgc.D3DDriver} **（ソフトウェアラスタライザへのフォールバック。取得時間は参考値）**");
                _report.Line($"- IsBorderRequired API 存在: {WgcCapture.IsBorderRequiredPresent}（TFM 19041 の投影には無いため未検証）");
            }

            _report.Line($"- モニタ数: {_monitors.Count}");
            foreach (MonitorInfo monitor in _monitors)
            {
                _report.Line($"  - {monitor.Label}");
            }

            var distinctDpi = _monitors.Select(m => m.Dpi).Distinct().ToList();
            _report.Line(distinctDpi.Count > 1
                ? $"- DPI 混在: あり ({string.Join(", ", distinctDpi)})"
                : "- DPI 混在: なし → AC-3 の DPI 混在ずれは **未計測（DPI 混在なし）**");
        }

        private async Task MeasureCaptureTimeAsync()
        {
            _report.Section($"静止フレーム取得時間（{_options.Iterations} 回の中央値、経路 {_source!.Name}）");
            _report.Line("| モニタ | 取得 px | 中央値 ms | 最小 ms | 最大 ms | 解像度一致 |");
            _report.Line("|---|---|---|---|---|---|");
            for (int i = 0; i < _monitors.Count; i++)
            {
                MonitorInfo monitor = _monitors[i];
                var samples = new List<double>();
                FrameData? last = null;
                for (int n = 0; n < _options.Iterations; n++)
                {
                    long start = Stopwatch.GetTimestamp();
                    last = await _source.CaptureAsync(monitor, includeCursor: false);
                    samples.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                }

                bool sizeMatch = last!.Width == monitor.Width && last.Height == monitor.Height;
                string png = Path.Combine(_options.OutDir, $"frame-{i}.png");
                last.SavePng(png);
                _report.Line($"| {monitor.DeviceName} | {last.Width}x{last.Height} | {Median(samples):0.0} | {samples.Min():0.0} | {samples.Max():0.0} | {(sizeMatch ? "OK" : "NG")} ({Path.GetFileName(png)}) |");
            }
        }

        /// <summary>トリガ → 全モニタ取得 → オーバーレイ表示完了 の遅延を計測し、最後の試行のオーバーレイを残す。</summary>
        private async Task<FrameData[]> MeasureOverlayAsync()
        {
            int iterations = _options.Auto ? _options.Iterations : 1;
            var captureMs = new List<double>();
            var totalMs = new List<double>();
            FrameData[] frames = [];
            for (int n = 0; n < iterations; n++)
            {
                var trigger = Stopwatch.StartNew();
                frames = new FrameData[_monitors.Count];
                for (int i = 0; i < _monitors.Count; i++)
                {
                    frames[i] = await _source!.CaptureAsync(_monitors[i], includeCursor: false);
                }

                captureMs.Add(trigger.Elapsed.TotalMilliseconds);

                for (int i = 0; i < _monitors.Count; i++)
                {
                    var overlay = new OverlayWindow(_monitors[i], frames[i], trigger);
                    overlay.Cancelled += OnCancelled;
                    _overlays.Add(overlay);
                    overlay.Show();
                }

                TimeSpan[] firstRenders = await Task.WhenAll(_overlays.Select(o => o.FirstRender.WaitAsync(TimeSpan.FromSeconds(10))));
                totalMs.Add(firstRenders.Max().TotalMilliseconds);

                bool last = n == iterations - 1;
                if (!last)
                {
                    foreach (OverlayWindow overlay in _overlays)
                    {
                        overlay.Close();
                    }

                    _overlays.Clear();
                    // 閉じた直後に次を取得するとオーバーレイ自身が写るため、デスクトップの再描画を待つ
                    await Task.Delay(300);
                }
            }

            _report.Section($"オーバーレイ表示遅延（{iterations} 回の中央値、モニタ {_monitors.Count} 台）");
            _report.Line($"- 全モニタ取得完了まで: 中央値 {Median(captureMs):0.0} ms（最小 {captureMs.Min():0.0} / 最大 {captureMs.Max():0.0}）");
            _report.Line($"- トリガ → 全オーバーレイ初回描画: 中央値 {Median(totalMs):0.0} ms（最小 {totalMs.Min():0.0} / 最大 {totalMs.Max():0.0}）");
            _report.Line($"- 目標 200 ms: {(Median(totalMs) <= 200 ? "達成" : "**超過**")}");
            return frames;
        }

        /// <summary>
        /// 各モニタの中央 256x256 について、フレームからの切り出しと「オーバーレイ表示中に同じ矩形を BitBlt したもの」を比較し、
        /// 表示 + 座標変換のずれを px で求める。あわせて Avalonia の PointToScreen が Win32 の期待値と一致するかを確認する。
        /// </summary>
        private async Task MeasureAlignmentAsync(FrameData[] frames)
        {
            await SettleRenderAsync();
            _report.Section("座標一致（DPI ごとのずれ）");
            _report.Line($"判定: |dx|,|dy| ≤ {_shiftTolerancePx} px を一致とみなす。{(_measurementsValid ? string.Empty : "**セルフチェック失敗のため無効**")}");
            _report.Line("| モニタ | DPI | scale | 検証矩形 (screen px) | ずれ (dx,dy) | PointToScreen 差 (px) | 判定 |");
            _report.Line("|---|---|---|---|---|---|---|");
            for (int i = 0; i < _monitors.Count; i++)
            {
                MonitorInfo monitor = _monitors[i];
                OverlayWindow overlay = _overlays[i];
                int size = Math.Min(_alignmentPatchSize, Math.Min(monitor.Width, monitor.Height) / 2);
                var screenRect = new PixelRect(
                    monitor.Bounds.Left + (monitor.Width - size) / 2,
                    monitor.Bounds.Top + (monitor.Height - size) / 2,
                    size,
                    size);

                FrameData expected = frames[i].Crop(overlay.ToFrameRect(screenRect));
                FrameData live = GdiCapture.CaptureRegion(screenRect.X, screenRect.Y, screenRect.Width, screenRect.Height);
                AlignmentResult result = Alignment.Find(expected, live);
                expected.SavePng(Path.Combine(_options.OutDir, $"align-{i}-expected.png"));
                live.SavePng(Path.Combine(_options.OutDir, $"align-{i}-live.png"));

                // 論理中央 → PointToScreen が物理中央に一致するか
                var logicalCenter = new Point(overlay.ClientSize.Width / 2, overlay.ClientSize.Height / 2);
                PixelPoint mapped = overlay.ToScreenPixel(logicalCenter);
                int expectedX = monitor.Bounds.Left + monitor.Width / 2;
                int expectedY = monitor.Bounds.Top + monitor.Height / 2;
                string pointDelta = $"({mapped.X - expectedX},{mapped.Y - expectedY})";

                bool pass = _measurementsValid && result.Matched && Math.Abs(result.Dx) <= _shiftTolerancePx && Math.Abs(result.Dy) <= _shiftTolerancePx
                    && Math.Abs(mapped.X - expectedX) <= _shiftTolerancePx && Math.Abs(mapped.Y - expectedY) <= _shiftTolerancePx;
                _report.Line($"| {monitor.DeviceName} | {monitor.Dpi} | {monitor.Scale:0.##} | {screenRect} | {result} | {pointDelta} | {(pass ? "一致" : "**不一致**")} |");
            }
        }

        private async Task MeasureCursorToggleAsync()
        {
            _report.Section("カーソル込み / 抜き");
            if (!_source!.SupportsCursorToggle)
            {
                _report.Line($"- {_source.Name} はカーソル切替に非対応（IsCursorCaptureEnabled 不在 or GDI）");
                return;
            }

            (int cx, int cy) = Win32.GetCursorPosition();
            int index = _monitors.FindIndex(m => cx >= m.Bounds.Left && cx < m.Bounds.Right && cy >= m.Bounds.Top && cy < m.Bounds.Bottom);
            if (index < 0)
            {
                _report.Line("- カーソル位置のモニタを特定できず未計測");
                return;
            }

            MonitorInfo monitor = _monitors[index];
            await SettleRenderAsync();
            FrameData with = await _source.CaptureAsync(monitor, includeCursor: true);
            FrameData without = await _source.CaptureAsync(monitor, includeCursor: false);
            with.SavePng(Path.Combine(_options.OutDir, "cursor-with.png"));
            without.SavePng(Path.Combine(_options.OutDir, "cursor-without.png"));
            long diff = FrameData.CountDifferentPixels(with, without);
            _report.Line($"- 対象モニタ: {monitor.DeviceName}、カーソル位置 ({cx},{cy})");
            _report.Line($"- 込み / 抜きの差分ピクセル数: {diff} → {(diff > 0 ? "切替可（IsCursorCaptureEnabled）" : "**差分なし（切替が効いていない可能性）**")}");
        }

        /// <summary>オーバーレイをマゼンタで塗ってキャプチャし、WDA_EXCLUDEFROMCAPTURE でその色が消えるかを見る。</summary>
        private async Task MeasureExclusionAsync()
        {
            _report.Section("オーバーレイ自身の除外（SetWindowDisplayAffinity / WDA_EXCLUDEFROMCAPTURE）");
            MonitorInfo monitor = _monitors.First(m => m.IsPrimary);
            OverlayWindow overlay = _overlays[_monitors.IndexOf(monitor)];
            try
            {
                overlay.ShowSentinel(true);
                await SettleRenderAsync();
                long before = (await _source!.CaptureAsync(monitor, includeCursor: false)).CountColor(0xFF, 0x00, 0xFF);

                bool applied = overlay.SetCaptureExclusion(true);
                await SettleRenderAsync();
                FrameData excluded = await _source.CaptureAsync(monitor, includeCursor: false);
                long after = excluded.CountColor(0xFF, 0x00, 0xFF);
                excluded.SavePng(Path.Combine(_options.OutDir, "exclusion-after.png"));

                _report.Line($"- 対象: {monitor.DeviceName}（{monitor.Width * monitor.Height} px）");
                _report.Line($"- 除外前のセンチネル画素数: {before}（0 だとキャプチャがオーバーレイを見ていない = 計測不能）");
                _report.Line($"- SetWindowDisplayAffinity 適用: {(applied ? "成功" : "**失敗**")}、除外後のセンチネル画素数: {after}");
                _report.Line($"- 判定: {(before > 0 && after == 0 ? "除外可" : before == 0 ? "計測不能" : "**除外不可**")}");
            }
            finally
            {
                overlay.SetCaptureExclusion(false);
                overlay.ShowSentinel(false);
            }
        }

        private void OnRegionSelected(OverlayWindow overlay, PixelRect requested)
        {
            // PointerReleased 派生の dispatcher ターンで動くため RunAsync の try/catch には入らない。
            // ここで捕捉しないと全画面 topmost を残したまま UI スレッド例外で落ちる
            try
            {
                _cropCount++;

                // Crop はフレーム外を無言でクランプする。隣モニタへ抜けたドラッグ等でクランプが起きると
                // 「保存 PNG」「BitBlt する矩形」「ログの rect」が食い違うので、入口で 1 度だけ交差させて全員に同じ矩形を渡す
                MonitorInfo monitor = overlay.Monitor;
                var monitorRect = new PixelRect(monitor.Bounds.Left, monitor.Bounds.Top, monitor.Width, monitor.Height);
                PixelRect screenRect = requested.Intersect(monitorRect);
                string clampNote = screenRect == requested ? string.Empty : $"（モニタ外をクランプ: 要求 {requested}）";
                if (screenRect.Width <= 0 || screenRect.Height <= 0)
                {
                    string skipped = $"- #{_cropCount} {monitor.DeviceName} rect={requested} → モニタ外のためスキップ";
                    _report.Line(skipped);
                    overlay.SetHud($"{monitor.Label}\n{skipped}\nドラッグで切り出し / Esc で終了");
                    return;
                }

                FrameData crop = overlay.Frame.Crop(overlay.ToFrameRect(screenRect));
                string png = Path.Combine(_options.OutDir, $"crop-{_cropCount}.png");
                crop.SavePng(png);

                string alignment = "（矩形が小さく比較不能）";
                if (crop.Width > Alignment.DefaultMaxShift * 2 + 1 && crop.Height > Alignment.DefaultMaxShift * 2 + 1)
                {
                    FrameData live = GdiCapture.CaptureRegion(screenRect.X, screenRect.Y, screenRect.Width, screenRect.Height);
                    alignment = Alignment.Find(crop, live).ToString();
                }

                string line = $"- #{_cropCount} {monitor.DeviceName} ({monitor.Dpi}dpi) rect={screenRect}{clampNote} → {Path.GetFileName(png)} ずれ {alignment}";
                _report.Line(line);
                Console.WriteLine(line);
                overlay.SetHud($"{monitor.Label}\n保存: {png}{clampNote}\nずれ: {alignment}\nドラッグで切り出し / Esc で終了");
            }
            catch (Exception ex)
            {
                _report.Line($"**ERROR**（ドラッグ #{_cropCount}）: {ex}");
                Console.Error.WriteLine(ex);
                Finish(exitCode: 1);
            }
        }

        private void DumpGeometryLog()
        {
            _report.Section("ウィンドウ配置ログ（Avalonia RenderScaling / 補正の有無）");
            for (int i = 0; i < _overlays.Count; i++)
            {
                _report.Line($"- {_monitors[i].DeviceName}:");
                foreach (string entry in _overlays[i].GeometryLog)
                {
                    _report.Line($"  - {entry}");
                }
            }
        }
    }

    /// <summary>Markdown 行の蓄積と書き出し（コンソールにも同時出力）。</summary>
    private sealed class Report
    {
        private readonly StringBuilder _lines = new();

        public Report(string filePath)
        {
            FilePath = filePath;
            _lines.AppendLine("# CaptureOverlay measurements");
            _lines.AppendLine();
        }

        public string FilePath { get; }

        public void Section(string title)
        {
            _lines.AppendLine();
            _lines.AppendLine($"## {title}");
            _lines.AppendLine();
            Console.WriteLine($"== {title}");
        }

        public void Line(string text)
        {
            _lines.AppendLine(text);
            Console.WriteLine(text);
        }

        public void Flush()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
            File.WriteAllText(FilePath, _lines.ToString());
        }
    }
}
