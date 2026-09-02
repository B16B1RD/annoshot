namespace CaptureOverlay;

/// <summary>コマンドライン引数。</summary>
internal sealed class SpikeOptions
{
    public const string Usage = """
        CaptureOverlay — Windows.Graphics.Capture + 全画面オーバーレイ spike

        使い方: dotnet run --project spikes/CaptureOverlay -- [options]

          --auto                無操作で全計測を実行し、出力先の measurements.md を書いて終了する
          --gdi                 取得経路を Windows.Graphics.Capture ではなく GDI BitBlt にする（Windows 上では WGC 未サポートでも続行する）
          --force-unsupported   IsSupported() の戻り値を false に差し替える（AC-6 の経路確認用。--gdi 併用時は Windows 上で GDI 経路が走る）
          --iterations N        取得時間 / 表示遅延の試行回数（既定 10）
          --out DIR             出力ディレクトリ（既定: 実行ファイルと同じ場所の out/。bin/ 配下、またはコピー実行時はコピー先配下。
                                git 管理下のパスを指定しないこと）
          --help                この説明を表示する

        既定（--auto なし）はオーバーレイを表示し、矩形ドラッグで切り出し PNG を保存する。Esc で終了。
        出力 PNG はデスクトップ全面のキャプチャ。確認後に削除し、リポジトリにコミットしないこと。
        """;

    public static SpikeOptions Current { get; set; } = new();

    public bool Auto { get; private set; }

    public bool UseGdi { get; private set; }

    public bool ForceUnsupported { get; private set; }

    public bool ShowHelp { get; private set; }

    public int Iterations { get; private set; } = 10;

    // CWD 相対にすると、文書化した `dotnet run --project ...` をリポジトリルートで実行したときに
    // デスクトップ全面 PNG が <repo-root>/out/ に落ちて git add -A で混入する。bin/ 配下に閉じる
    public string OutDir { get; private set; } = Path.Combine(AppContext.BaseDirectory, "out");

    public static SpikeOptions Parse(string[] args)
    {
        var options = new SpikeOptions();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--auto":
                    options.Auto = true;
                    break;
                case "--gdi":
                    options.UseGdi = true;
                    break;
                case "--force-unsupported":
                    options.ForceUnsupported = true;
                    break;
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
                case "--iterations":
                    options.Iterations = ParseIntValue(args, ref i);
                    break;
                case "--out":
                    options.OutDir = RequireValue(args, ref i);
                    break;
                default:
                    throw new ArgumentException($"不明な引数: {args[i]}");
            }
        }

        return options;
    }

    private static string RequireValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{args[index]} には値が必要です");
        }

        index++;
        return args[index];
    }

    private static int ParseIntValue(string[] args, ref int index)
    {
        string name = args[index];
        string value = RequireValue(args, ref index);
        if (!int.TryParse(value, out int parsed) || parsed <= 0)
        {
            throw new ArgumentException($"{name} には正の整数を指定してください: {value}");
        }

        return parsed;
    }
}
