using CaptureOverlay.Native;

namespace CaptureOverlay.Capture;

/// <summary>GDI BitBlt による取得。WGC 未サポート時の代替候補、およびオーバーレイ表示中の「ずれ計測」用ライブ取得に使う。</summary>
internal sealed class GdiCapture : IFrameSource
{
    public string Name => "GDI BitBlt";

    // BitBlt はカーソルを含まない。DrawIconEx で合成する実装は spike の範囲外
    public bool SupportsCursorToggle => false;

    public Task<FrameData> CaptureAsync(MonitorInfo monitor, bool includeCursor)
        => Task.FromResult(CaptureRegion(monitor.Bounds.Left, monitor.Bounds.Top, monitor.Width, monitor.Height));

    public static FrameData CaptureRegion(int x, int y, int width, int height)
        => new(width, height, Win32.CaptureScreenRegion(x, y, width, height));

    public void Dispose()
    {
    }
}
