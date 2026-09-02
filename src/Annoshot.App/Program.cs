using Avalonia;

namespace Annoshot.App;

internal static class Program
{
    // Avalonia の初期化前に Avalonia API / SynchronizationContext 依存コードを呼ばないこと。
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // ビジュアルデザイナーからも参照されるため削除しない。
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
