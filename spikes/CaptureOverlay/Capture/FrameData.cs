using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace CaptureOverlay.Capture;

/// <summary>CPU 側の BGRA8 フレーム（stride = Width * 4、上から下）。</summary>
internal sealed class FrameData
{
    public FrameData(int width, int height, byte[] bgra)
    {
        if (bgra.Length != width * height * 4)
        {
            throw new ArgumentException($"バッファ長 {bgra.Length} が {width}x{height}x4 と一致しません", nameof(bgra));
        }

        Width = width;
        Height = height;
        Pixels = bgra;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    /// <summary>フレーム内ピクセル座標の矩形を切り出す（フレーム外は clamp）。</summary>
    public FrameData Crop(PixelRect rect)
    {
        int x0 = Math.Clamp(rect.X, 0, Width);
        int y0 = Math.Clamp(rect.Y, 0, Height);
        int x1 = Math.Clamp(rect.Right, 0, Width);
        int y1 = Math.Clamp(rect.Bottom, 0, Height);
        int w = Math.Max(0, x1 - x0);
        int h = Math.Max(0, y1 - y0);
        var dst = new byte[w * h * 4];
        for (int row = 0; row < h; row++)
        {
            Buffer.BlockCopy(Pixels, ((y0 + row) * Width + x0) * 4, dst, row * w * 4, w * 4);
        }

        return new FrameData(w, h, dst);
    }

    public WriteableBitmap ToBitmap()
    {
        var bitmap = new WriteableBitmap(new PixelSize(Width, Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using ILockedFramebuffer fb = bitmap.Lock();
        int rowBytes = Width * 4;
        for (int y = 0; y < Height; y++)
        {
            Marshal.Copy(Pixels, y * rowBytes, fb.Address + y * fb.RowBytes, rowBytes);
        }

        return bitmap;
    }

    public void SavePng(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using WriteableBitmap bitmap = ToBitmap();
        bitmap.Save(path);
    }

    /// <summary>指定色（B,G,R）と一致するピクセル数。除外テストのセンチネル検出に使う。</summary>
    public long CountColor(byte b, byte g, byte r, int tolerance = 8)
    {
        long count = 0;
        for (int i = 0; i < Pixels.Length; i += 4)
        {
            if (Math.Abs(Pixels[i] - b) <= tolerance && Math.Abs(Pixels[i + 1] - g) <= tolerance && Math.Abs(Pixels[i + 2] - r) <= tolerance)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>同サイズ 2 フレームで差の大きいピクセル数を数える。カーソル込み/抜きの判定に使う。</summary>
    public static long CountDifferentPixels(FrameData a, FrameData b, int tolerance = 16)
    {
        if (a.Width != b.Width || a.Height != b.Height)
        {
            throw new ArgumentException("フレームサイズが一致しません");
        }

        long count = 0;
        for (int i = 0; i < a.Pixels.Length; i += 4)
        {
            if (Math.Abs(a.Pixels[i] - b.Pixels[i]) > tolerance
                || Math.Abs(a.Pixels[i + 1] - b.Pixels[i + 1]) > tolerance
                || Math.Abs(a.Pixels[i + 2] - b.Pixels[i + 2]) > tolerance)
            {
                count++;
            }
        }

        return count;
    }
}
