using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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
            // メインループ開始後に起動する: 初期化が同期的に失敗して即 Shutdown(1) する経路で、
            // ループ開始前に Shutdown すると Avalonia 側が「Dispatcher shut down」で未処理例外になるため
            Dispatcher.UIThread.Post(() => _ = SpikeRunner.RunAsync(desktop, SpikeOptions.Current));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
