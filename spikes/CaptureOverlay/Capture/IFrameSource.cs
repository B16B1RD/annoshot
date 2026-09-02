using CaptureOverlay.Native;

namespace CaptureOverlay.Capture;

/// <summary>モニタ 1 台の静止フレームを 1 枚取得する。</summary>
internal interface IFrameSource : IDisposable
{
    string Name { get; }

    /// <summary>カーソル込み / 抜きの切替に対応しているか。</summary>
    bool SupportsCursorToggle { get; }

    Task<FrameData> CaptureAsync(MonitorInfo monitor, bool includeCursor);
}
