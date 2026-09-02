using Avalonia;
using Avalonia.Controls;
using CaptureOverlay.Capture;

namespace CaptureOverlay;

internal static class Program
{
    // Avalonia の初期化前に Avalonia API / SynchronizationContext 依存コードを呼ばないこと。
    [STAThread]
    public static int Main(string[] args)
    {
        SpikeOptions options;
        try
        {
            options = SpikeOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(SpikeOptions.Usage);
            return 2;
        }

        if (options.ShowHelp)
        {
            Console.WriteLine(SpikeOptions.Usage);
            return 0;
        }

        // AC-6: 未サポート環境では Avalonia を起動せずメッセージを出して正常終了する。
        // Probe は Windows.* 型を参照しないメソッド内で判定を完結させ、WSL でもこの経路を通せる。
        // --gdi 指定時は WGC の可否に依存しないため、Windows 上なら未サポートでも GDI 経路で続行する。
        // 非 Windows は GDI（user32/gdi32）も Avalonia の Win32 バックエンドも動かないため、理由を問わず終了する。
        CaptureSupport support = CaptureSupport.Probe(options.ForceUnsupported);
        if (!support.IsSupported)
        {
            Console.WriteLine($"Windows.Graphics.Capture は未サポートです（理由: {support.Reason}）。");
            if (!OperatingSystem.IsWindows())
            {
                Console.WriteLine("本 spike は Windows 専用です。GDI BitBlt 経路（--gdi）も Windows 上でのみ動作します。");
                return 0;
            }

            if (!options.UseGdi)
            {
                Console.WriteLine("代替: --gdi を付けて GDI BitBlt 経路で再実行するか、Desktop Duplication の採用を検討してください。");
                return 0;
            }

            Console.WriteLine("--gdi 指定のため GDI BitBlt 経路で続行します。");
        }

        SpikeOptions.Current = options;
        // 計測失敗時は SpikeRunner が Shutdown(1) するため、終了コードをそのまま返す
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
