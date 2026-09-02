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
        CaptureSupport support = CaptureSupport.Probe(options.ForceUnsupported);
        if (!support.IsSupported)
        {
            Console.WriteLine($"Windows.Graphics.Capture は未サポートです（理由: {support.Reason}）。");
            Console.WriteLine("代替: GDI BitBlt（--gdi）または Desktop Duplication の採用を検討してください。");
            return 0;
        }

        SpikeOptions.Current = options;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
