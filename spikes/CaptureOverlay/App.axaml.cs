using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CaptureOverlay.Measure;

namespace CaptureOverlay;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // MainWindow は持たない。SpikeRunner がモニタごとのオーバーレイを作り、完了時に明示 Shutdown する。
            _ = SpikeRunner.RunAsync(desktop, SpikeOptions.Current);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
