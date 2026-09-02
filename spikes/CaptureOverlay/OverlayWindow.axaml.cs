using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.VisualTree;
using CaptureOverlay.Capture;
using CaptureOverlay.Native;

namespace CaptureOverlay;

/// <summary>
/// モニタ 1 台を全面に覆うオーバーレイ。取得した静止フレームを 1:1 で表示し、矩形ドラッグで物理ピクセル矩形を返す。
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly MonitorInfo _monitor;
    private readonly TaskCompletionSource<TimeSpan> _firstRender = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<string> _geometryLog = new();
    private readonly Stopwatch _trigger;
    private Point? _dragStart;

    // Avalonia デザイナ用（実行時は使わない）
    public OverlayWindow()
        : this(new MonitorInfo(IntPtr.Zero, "design", default, true, 96), new FrameData(1, 1, new byte[4]), Stopwatch.StartNew())
    {
    }

    internal OverlayWindow(MonitorInfo monitor, FrameData frame, Stopwatch trigger)
    {
        _monitor = monitor;
        _trigger = trigger;
        InitializeComponent();

        Frame = frame;
        FrameImage.Source = frame.ToBitmap();

        // 表示前は RenderScaling が確定しないため、Win32 の実効 DPI から論理サイズを決めて置く。
        // 表示後に EnsureGeometry で実測スケールと突き合わせて補正する（補正の有無が RESULT の材料）。
        Position = new PixelPoint(monitor.Bounds.Left, monitor.Bounds.Top);
        Width = monitor.Width / monitor.Scale;
        Height = monitor.Height / monitor.Scale;

        Opened += OnOpened;
        ScalingChanged += (_, _) => EnsureGeometry("ScalingChanged");
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        KeyDown += OnKeyDown;
    }

    /// <summary>ドラッグ確定。矩形は物理ピクセル（仮想スクリーン座標）。</summary>
    internal event Action<OverlayWindow, PixelRect>? RegionSelected;

    /// <summary>Esc で中断。</summary>
    internal event Action? Cancelled;

    internal FrameData Frame { get; }

    internal MonitorInfo Monitor => _monitor;

    /// <summary>トリガ起点から最初の描画フレームまでの経過時間。</summary>
    internal Task<TimeSpan> FirstRender => _firstRender.Task;

    internal IReadOnlyList<string> GeometryLog => _geometryLog;

    internal void SetHud(string text) => Hud.Text = text;

    internal void ShowSentinel(bool visible) => Sentinel.IsVisible = visible;

    /// <summary>WDA_EXCLUDEFROMCAPTURE を適用 / 解除する。HWND が取れない場合は false。</summary>
    internal bool SetCaptureExclusion(bool exclude)
    {
        IPlatformHandle? handle = TryGetPlatformHandle();
        return handle is not null && Win32.SetCaptureExclusion(handle.Handle, exclude);
    }

    /// <summary>ウィンドウ論理座標 → 物理ピクセル（仮想スクリーン座標）。Avalonia の変換をそのまま使う。</summary>
    internal PixelPoint ToScreenPixel(Point logical) => this.PointToScreen(logical);

    /// <summary>物理ピクセル矩形（仮想スクリーン座標）→ このモニタのフレーム内座標。</summary>
    internal PixelRect ToFrameRect(PixelRect screenRect)
        => new(screenRect.X - _monitor.Bounds.Left, screenRect.Y - _monitor.Bounds.Top, screenRect.Width, screenRect.Height);

    private void OnOpened(object? sender, EventArgs e)
    {
        EnsureGeometry("Opened");

        // RequestAnimationFrame は次の描画パス直前に呼ばれる。2 回目を「最初のフレームが画面に出た」時点とみなす
        RequestAnimationFrame(_ => RequestAnimationFrame(_ => _firstRender.TrySetResult(_trigger.Elapsed)));
    }

    private void EnsureGeometry(string reason)
    {
        double scale = RenderScaling;
        double expectedWidth = _monitor.Width / scale;
        double expectedHeight = _monitor.Height / scale;
        var expectedPosition = new PixelPoint(_monitor.Bounds.Left, _monitor.Bounds.Top);

        Screen? screen = Screens.ScreenFromWindow(this);
        _geometryLog.Add(
            $"[{reason}] RenderScaling={scale:0.###} (Win32 {_monitor.Scale:0.###}) " +
            $"ClientSize={ClientSize.Width:0.#}x{ClientSize.Height:0.#} Position={Position} " +
            $"AvaloniaScreen={(screen is null ? "null" : $"{screen.Bounds} scaling={screen.Scaling:0.###}")}");

        bool sizeMismatch = Math.Abs(Width - expectedWidth) > 0.01 || Math.Abs(Height - expectedHeight) > 0.01;
        if (sizeMismatch)
        {
            Width = expectedWidth;
            Height = expectedHeight;
            _geometryLog.Add($"[{reason}] size corrected -> {expectedWidth:0.##}x{expectedHeight:0.##}");
        }

        if (Position != expectedPosition)
        {
            Position = expectedPosition;
            _geometryLog.Add($"[{reason}] position corrected -> {expectedPosition}");
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragStart = e.GetPosition(this);
        Selection.IsVisible = true;
        UpdateSelection(_dragStart.Value);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragStart is null)
        {
            return;
        }

        UpdateSelection(e.GetPosition(this));
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragStart is null)
        {
            return;
        }

        Point start = _dragStart.Value;
        Point end = e.GetPosition(this);
        _dragStart = null;
        Selection.IsVisible = false;

        PixelPoint p1 = ToScreenPixel(start);
        PixelPoint p2 = ToScreenPixel(end);
        int x = Math.Min(p1.X, p2.X);
        int y = Math.Min(p1.Y, p2.Y);
        int w = Math.Abs(p1.X - p2.X);
        int h = Math.Abs(p1.Y - p2.Y);
        if (w >= 4 && h >= 4)
        {
            RegionSelected?.Invoke(this, new PixelRect(x, y, w, h));
        }
    }

    private void UpdateSelection(Point current)
    {
        Point start = _dragStart!.Value;
        double x = Math.Min(start.X, current.X);
        double y = Math.Min(start.Y, current.Y);
        Canvas.SetLeft(Selection, x);
        Canvas.SetTop(Selection, y);
        Selection.Width = Math.Abs(start.X - current.X);
        Selection.Height = Math.Abs(start.Y - current.Y);

        PixelPoint px = ToScreenPixel(current);
        SetHud($"{_monitor.Label}\nlogical=({current.X:0.#},{current.Y:0.#}) screen px=({px.X},{px.Y}) scale={RenderScaling:0.###}\nドラッグで切り出し / Esc で終了");
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Cancelled?.Invoke();
        }
    }
}
