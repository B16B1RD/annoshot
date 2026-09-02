using System.Runtime.CompilerServices;
using Windows.Graphics.Capture;

namespace CaptureOverlay.Capture;

/// <summary>Windows.Graphics.Capture の利用可否。</summary>
internal sealed record CaptureSupport(bool IsSupported, string Reason)
{
    /// <summary>
    /// 利用可否を判定する。<paramref name="forceUnsupported"/> は <c>IsSupported()</c> の戻り値を false に差し替えるだけで、
    /// 判定後の分岐は実機経路と同一。Windows.* 型の参照は <see cref="QueryRuntime"/> に閉じ込め、
    /// 非 Windows（WSL）でも本メソッドは型ロードなしに完走する。
    /// </summary>
    public static CaptureSupport Probe(bool forceUnsupported)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new CaptureSupport(false, $"Windows ではありません ({Environment.OSVersion})");
        }

        // GraphicsCaptureSession.IsSupported() 自体が 1903 (build 18362) 以降にしか存在しない
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362))
        {
            return new CaptureSupport(false, $"OS ビルドが古い ({Environment.OSVersion.Version})");
        }

        bool supported;
        try
        {
            supported = forceUnsupported ? false : QueryRuntime();
        }
        catch (Exception ex)
        {
            return new CaptureSupport(false, $"IsSupported() の呼び出しに失敗: {ex.GetType().Name}: {ex.Message}");
        }

        return supported
            ? new CaptureSupport(true, "GraphicsCaptureSession.IsSupported() == true")
            : new CaptureSupport(false, forceUnsupported ? "--force-unsupported 指定" : "GraphicsCaptureSession.IsSupported() == false");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool QueryRuntime() => GraphicsCaptureSession.IsSupported();
}
