using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using CaptureOverlay.Native;
using Windows.Foundation.Metadata;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using WinRT;

namespace CaptureOverlay.Capture;

/// <summary>
/// Windows.Graphics.Capture でモニタ 1 台の静止フレームを 1 枚取得する。
/// GPU サーフェスの CPU 読み出しは D3D11 の staging texture ではなく
/// <see cref="SoftwareBitmap.CreateCopyFromSurfaceAsync(IDirect3DSurface, BitmapAlphaMode)"/> に任せる（外部ライブラリ不要）。
/// </summary>
internal sealed class WgcCapture : IFrameSource
{
    private static readonly Guid _graphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid _graphicsCaptureItemInteropIid = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    private static readonly TimeSpan _firstFrameTimeout = TimeSpan.FromSeconds(5);

    private readonly IDirect3DDevice _device;
    private readonly IGraphicsCaptureItemInterop _interop;

    public WgcCapture()
    {
        IntPtr devicePtr = Win32.CreateWinRtDirect3DDevice();
        try
        {
            _device = MarshalInterface<IDirect3DDevice>.FromAbi(devicePtr);
        }
        finally
        {
            Marshal.Release(devicePtr);
        }

        IntPtr factoryPtr = Win32.GetActivationFactory("Windows.Graphics.Capture.GraphicsCaptureItem", _graphicsCaptureItemInteropIid);
        try
        {
            _interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
        }
        finally
        {
            Marshal.Release(factoryPtr);
        }

        SupportsCursorToggle = ApiInformation.IsPropertyPresent("Windows.Graphics.Capture.GraphicsCaptureSession", "IsCursorCaptureEnabled");
    }

    // IGraphicsCaptureItemInterop は WinRT 投影に含まれない COM インターフェイスなので手で宣言する
    [ComImport]
    [System.Runtime.InteropServices.Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow(IntPtr window, ref Guid iid);

        IntPtr CreateForMonitor(IntPtr monitor, ref Guid iid);
    }

    public string Name => "Windows.Graphics.Capture";

    public bool SupportsCursorToggle { get; }

    /// <summary>IsBorderRequired（黄枠の抑止）は 10.0.20348 以降の投影にしか無く、本 spike の TFM (19041) では型に現れない。</summary>
    public static bool IsBorderRequiredPresent
        => ApiInformation.IsPropertyPresent("Windows.Graphics.Capture.GraphicsCaptureSession", "IsBorderRequired");

    public async Task<FrameData> CaptureAsync(MonitorInfo monitor, bool includeCursor)
    {
        GraphicsCaptureItem item = CreateItemForMonitor(monitor.Handle);
        using var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(_device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, item.Size);
        using GraphicsCaptureSession session = pool.CreateCaptureSession(item);
        if (SupportsCursorToggle)
        {
            session.IsCursorCaptureEnabled = includeCursor;
        }

        var firstFrame = new TaskCompletionSource<Direct3D11CaptureFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        pool.FrameArrived += (sender, _) =>
        {
            Direct3D11CaptureFrame? frame = sender.TryGetNextFrame();
            if (frame is not null && !firstFrame.TrySetResult(frame))
            {
                frame.Dispose();
            }
        };

        session.StartCapture();
        using Direct3D11CaptureFrame captured = await firstFrame.Task.WaitAsync(_firstFrameTimeout).ConfigureAwait(false);

        SoftwareBitmap bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(captured.Surface, BitmapAlphaMode.Premultiplied);
        if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
        {
            SoftwareBitmap converted = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            bitmap.Dispose();
            bitmap = converted;
        }

        using (bitmap)
        {
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;
            var buffer = new Windows.Storage.Streams.Buffer((uint)(width * height * 4));
            bitmap.CopyToBuffer(buffer);
            return new FrameData(width, height, buffer.ToArray());
        }
    }

    public void Dispose()
    {
        _device.Dispose();
    }

    private GraphicsCaptureItem CreateItemForMonitor(IntPtr hMonitor)
    {
        Guid iid = _graphicsCaptureItemIid;
        IntPtr itemPtr = _interop.CreateForMonitor(hMonitor, ref iid);
        try
        {
            return GraphicsCaptureItem.FromAbi(itemPtr);
        }
        finally
        {
            Marshal.Release(itemPtr);
        }
    }
}
