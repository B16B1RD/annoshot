using CaptureOverlay.Capture;

namespace CaptureOverlay.Measure;

internal readonly record struct AlignmentResult(int Dx, int Dy, double MeanAbsDiff, bool Matched)
{
    public override string ToString() => Matched ? $"dx={Dx} dy={Dy} (diff={MeanAbsDiff:0.00})" : $"一致なし (best dx={Dx} dy={Dy} diff={MeanAbsDiff:0.00})";
}

/// <summary>
/// 「フレームから切り出した矩形」と「オーバーレイ表示中に同じ矩形を BitBlt したもの」を比較し、
/// 表示 / 座標変換のずれを px 単位で求める。<c>candidate(x, y) ≈ reference(x + dx, y + dy)</c> となる (dx, dy) を探す。
/// </summary>
internal static class Alignment
{
    public const int DefaultMaxShift = 8;

    /// <summary>平均絶対差（0..255）がこの値未満なら一致とみなす。</summary>
    public const double MatchThreshold = 6.0;

    public static AlignmentResult Find(FrameData reference, FrameData candidate, int maxShift = DefaultMaxShift)
    {
        if (reference.Width != candidate.Width || reference.Height != candidate.Height)
        {
            throw new ArgumentException("比較する 2 画像のサイズが一致しません");
        }

        int w = reference.Width;
        int h = reference.Height;
        if (w <= maxShift * 2 + 1 || h <= maxShift * 2 + 1)
        {
            throw new ArgumentException($"画像が小さすぎます（{w}x{h}、探索幅 ±{maxShift}）");
        }

        int bestDx = 0;
        int bestDy = 0;
        double bestDiff = double.MaxValue;
        for (int dy = -maxShift; dy <= maxShift; dy++)
        {
            for (int dx = -maxShift; dx <= maxShift; dx++)
            {
                double diff = MeanAbsDiff(reference, candidate, dx, dy, maxShift);
                // 同点なら原点に近い方を採る（ノイズで無意味なオフセットを選ばない）
                if (diff < bestDiff - 1e-9 || (Math.Abs(diff - bestDiff) <= 1e-9 && Math.Abs(dx) + Math.Abs(dy) < Math.Abs(bestDx) + Math.Abs(bestDy)))
                {
                    bestDiff = diff;
                    bestDx = dx;
                    bestDy = dy;
                }
            }
        }

        return new AlignmentResult(bestDx, bestDy, bestDiff, bestDiff < MatchThreshold);
    }

    /// <summary>
    /// 既知オフセットの合成画像で <see cref="Find"/> がそのオフセットを復元できることを確認する。
    /// 1 ケースでも外れれば false（呼び出し側は以降の計測値を「無効」とマークする）。
    /// </summary>
    public static bool SelfCheck(out string detail)
    {
        (int Dx, int Dy)[] cases = [(0, 0), (3, -2), (-7, 5), (DefaultMaxShift, DefaultMaxShift)];
        const int size = 96;
        FrameData reference = SyntheticNoise(size, size, seed: 42);
        var lines = new List<string>();
        bool ok = true;
        foreach ((int dx, int dy) in cases)
        {
            FrameData shifted = Shift(reference, dx, dy);
            AlignmentResult result = Find(reference, shifted);
            bool pass = result.Matched && result.Dx == dx && result.Dy == dy;
            ok &= pass;
            lines.Add($"expected ({dx},{dy}) -> got {result} {(pass ? "OK" : "FAIL")}");
        }

        detail = string.Join("; ", lines);
        return ok;
    }

    private static double MeanAbsDiff(FrameData reference, FrameData candidate, int dx, int dy, int margin)
    {
        int w = reference.Width;
        long sum = 0;
        long count = 0;
        for (int y = margin; y < reference.Height - margin; y++)
        {
            int refRow = (y + dy) * w;
            int candRow = y * w;
            for (int x = margin; x < w - margin; x++)
            {
                int r = (refRow + x + dx) * 4;
                int c = (candRow + x) * 4;
                sum += Math.Abs(reference.Pixels[r] - candidate.Pixels[c])
                     + Math.Abs(reference.Pixels[r + 1] - candidate.Pixels[c + 1])
                     + Math.Abs(reference.Pixels[r + 2] - candidate.Pixels[c + 2]);
                count += 3;
            }
        }

        return count == 0 ? double.MaxValue : (double)sum / count;
    }

    private static FrameData SyntheticNoise(int width, int height, int seed)
    {
        var random = new Random(seed);
        var pixels = new byte[width * height * 4];
        random.NextBytes(pixels);
        for (int i = 3; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;
        }

        return new FrameData(width, height, pixels);
    }

    /// <summary>shifted(x, y) = source(x + dx, y + dy)。範囲外は黒。</summary>
    private static FrameData Shift(FrameData source, int dx, int dy)
    {
        int w = source.Width;
        int h = source.Height;
        var pixels = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        {
            int sy = y + dy;
            if (sy < 0 || sy >= h)
            {
                continue;
            }

            for (int x = 0; x < w; x++)
            {
                int sx = x + dx;
                if (sx < 0 || sx >= w)
                {
                    continue;
                }

                Buffer.BlockCopy(source.Pixels, (sy * w + sx) * 4, pixels, (y * w + x) * 4, 4);
            }
        }

        return new FrameData(w, h, pixels);
    }
}
