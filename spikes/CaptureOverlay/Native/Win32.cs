using System.Runtime.InteropServices;

namespace CaptureOverlay.Native;

/// <summary>spike で必要な最小限の Win32 / WinRT 起動 API。</summary>
internal static class Win32
{
    public const uint WdaNone = 0x0;
    public const uint WdaExcludeFromCapture = 0x11;

    private const uint _monitorinfofPrimary = 0x1;
    private const uint _srcCopy = 0x00CC0020;
    private const uint _captureBlt = 0x40000000;
    private const uint _biRgb = 0;
    private const uint _dibRgbColors = 0;
    private const int _mdtEffectiveDpi = 0;

    private const int _d3DDriverTypeHardware = 1;
    private const int _d3DDriverTypeWarp = 5;
    private const uint _d3D11CreateDeviceBgraSupport = 0x20;
    private const uint _d3D11SdkVersion = 7;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

    /// <summary>接続中のモニタを列挙する（物理ピクセル座標 + 実効 DPI）。</summary>
    public static List<MonitorInfo> EnumerateMonitors()
    {
        var monitors = new List<MonitorInfo>();
        MonitorEnumProc callback = (IntPtr hMonitor, IntPtr hdc, ref Rect rect, IntPtr data) =>
        {
            var info = new MonitorInfoEx { CbSize = (uint)Marshal.SizeOf<MonitorInfoEx>() };
            if (!GetMonitorInfoW(hMonitor, ref info))
            {
                return true;
            }

            uint dpi = 96;
            if (GetDpiForMonitor(hMonitor, _mdtEffectiveDpi, out uint dpiX, out uint _) == 0)
            {
                dpi = dpiX;
            }

            monitors.Add(new MonitorInfo(
                hMonitor,
                info.SzDevice,
                info.RcMonitor,
                (info.DwFlags & _monitorinfofPrimary) != 0,
                dpi));
            return true;
        };

        if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero))
        {
            throw new InvalidOperationException("EnumDisplayMonitors に失敗しました");
        }

        GC.KeepAlive(callback);
        return monitors;
    }

    /// <summary>画面の指定領域（物理ピクセル、仮想スクリーン座標）を GDI BitBlt で取得する。BGRA 上下正順。</summary>
    public static byte[] CaptureScreenRegion(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "取得領域のサイズは正である必要があります");
        }

        IntPtr screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new InvalidOperationException("GetDC(NULL) に失敗しました");
        }

        IntPtr memDc = IntPtr.Zero;
        IntPtr bitmap = IntPtr.Zero;
        IntPtr previous = IntPtr.Zero;
        try
        {
            memDc = CreateCompatibleDC(screenDc);
            bitmap = CreateCompatibleBitmap(screenDc, width, height);
            if (memDc == IntPtr.Zero || bitmap == IntPtr.Zero)
            {
                throw new InvalidOperationException("互換 DC / ビットマップの作成に失敗しました");
            }

            previous = SelectObject(memDc, bitmap);
            if (!BitBlt(memDc, 0, 0, width, height, screenDc, x, y, _srcCopy | _captureBlt))
            {
                throw new InvalidOperationException($"BitBlt に失敗しました (Win32 error {Marshal.GetLastWin32Error()})");
            }

            var header = new BitmapInfoHeader
            {
                BiSize = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                BiWidth = width,
                // 負の高さで top-down（上の行が先頭）にする
                BiHeight = -height,
                BiPlanes = 1,
                BiBitCount = 32,
                BiCompression = _biRgb,
            };
            var pixels = new byte[width * height * 4];
            int lines = GetDIBits(memDc, bitmap, 0, (uint)height, pixels, ref header, _dibRgbColors);
            if (lines != height)
            {
                throw new InvalidOperationException($"GetDIBits が {lines}/{height} 行しか返しませんでした");
            }

            // GDI の 32bpp は alpha が未定義（多くは 0）なので不透明に正規化する
            for (int i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
            }

            return pixels;
        }
        finally
        {
            if (previous != IntPtr.Zero)
            {
                SelectObject(memDc, previous);
            }

            if (bitmap != IntPtr.Zero)
            {
                DeleteObject(bitmap);
            }

            if (memDc != IntPtr.Zero)
            {
                DeleteDC(memDc);
            }

            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    /// <summary>ウィンドウをキャプチャから除外（WDA_EXCLUDEFROMCAPTURE）/ 解除する。戻り値は成否。</summary>
    public static bool SetCaptureExclusion(IntPtr hwnd, bool exclude)
        => SetWindowDisplayAffinity(hwnd, exclude ? WdaExcludeFromCapture : WdaNone);

    public static (int X, int Y) GetCursorPosition()
    {
        return GetCursorPos(out Point point) ? (point.X, point.Y) : (0, 0);
    }

    /// <summary>WinRT の activation factory を IUnknown ポインタで取得する（呼び出し側が Release する）。</summary>
    public static IntPtr GetActivationFactory(string runtimeClassName, Guid factoryIid)
    {
        int hr = WindowsCreateString(runtimeClassName, runtimeClassName.Length, out IntPtr hstring);
        Marshal.ThrowExceptionForHR(hr);
        try
        {
            hr = RoGetActivationFactory(hstring, ref factoryIid, out IntPtr factory);
            Marshal.ThrowExceptionForHR(hr);
            return factory;
        }
        finally
        {
            WindowsDeleteString(hstring);
        }
    }

    /// <summary>D3D11 デバイスを作り IDXGIDevice 経由で WinRT IDirect3DDevice（IInspectable ポインタ）に変換する。</summary>
    public static IntPtr CreateWinRtDirect3DDevice()
    {
        var dxgiDeviceIid = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
        IntPtr device = IntPtr.Zero;
        IntPtr context = IntPtr.Zero;
        IntPtr dxgiDevice = IntPtr.Zero;
        try
        {
            int hr = D3D11CreateDevice(IntPtr.Zero, _d3DDriverTypeHardware, IntPtr.Zero, _d3D11CreateDeviceBgraSupport,
                IntPtr.Zero, 0, _d3D11SdkVersion, out device, out int _, out context);
            if (hr < 0)
            {
                // GPU ドライバ無し（リモートデスクトップ等）では WARP に落とす
                hr = D3D11CreateDevice(IntPtr.Zero, _d3DDriverTypeWarp, IntPtr.Zero, _d3D11CreateDeviceBgraSupport,
                    IntPtr.Zero, 0, _d3D11SdkVersion, out device, out int _, out context);
            }

            Marshal.ThrowExceptionForHR(hr);
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(device, ref dxgiDeviceIid, out dxgiDevice));
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out IntPtr inspectable));
            return inspectable;
        }
        finally
        {
            if (dxgiDevice != IntPtr.Zero)
            {
                Marshal.Release(dxgiDevice);
            }

            if (context != IntPtr.Zero)
            {
                Marshal.Release(context);
            }

            if (device != IntPtr.Zero)
            {
                Marshal.Release(device);
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, uint rop);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines, byte[] lpvBits, ref BitmapInfoHeader lpbmi, uint usage);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr ho);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string sourceString, int length, out IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags,
        IntPtr featureLevels, uint featureLevelCount, uint sdkVersion, out IntPtr device, out int featureLevel, out IntPtr immediateContext);

    [DllImport("d3d11.dll")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;

        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public uint CbSize;
        public Rect RcMonitor;
        public Rect RcWork;
        public uint DwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string SzDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint BiSize;
        public int BiWidth;
        public int BiHeight;
        public ushort BiPlanes;
        public ushort BiBitCount;
        public uint BiCompression;
        public uint BiSizeImage;
        public int BiXPelsPerMeter;
        public int BiYPelsPerMeter;
        public uint BiClrUsed;
        public uint BiClrImportant;
    }
}

/// <summary>Win32 から見たモニタ 1 台分の情報。座標は物理ピクセル（仮想スクリーン座標）。</summary>
internal sealed record MonitorInfo(IntPtr Handle, string DeviceName, Win32.Rect Bounds, bool IsPrimary, uint Dpi)
{
    public int Width => Bounds.Width;

    public int Height => Bounds.Height;

    public double Scale => Dpi / 96.0;

    public string Label => $"{DeviceName} {Width}x{Height} @({Bounds.Left},{Bounds.Top}) {Dpi}dpi ({Scale * 100:0}%)" + (IsPrimary ? " primary" : string.Empty);
}
