# annoshot

Windows 用の操作説明ビジュアル制作ツール。
注釈付きスクリーンショットとショート動画を、キャプチャ → 検閲 → 注釈 → クリップボードまで一続きの体験で作ることを目的とする。
[macshot](https://github.com/sw33tLie/macshot) (macOS) に着想を得ている。

- 探索サマリ（目的・スコープ・却下案・未解決の問い）: [docs/exploration-summary.md](docs/exploration-summary.md)

## 技術スタック

- .NET 8 / C#
- Avalonia 11（デスクトップ UI）
- xUnit（テスト）

## 構成

| プロジェクト | TFM | 役割 |
|---|---|---|
| `src/Annoshot.Core` | `net8.0` | UI・OS 非依存のドメインロジック。Windows 固有の参照を持たない |
| `src/Annoshot.Windows` | `net8.0-windows10.0.19041.0` | WinRT / Win32 のラッパ |
| `src/Annoshot.App` | `net8.0-windows10.0.19041.0` | Avalonia デスクトップアプリ本体 |
| `tests/Annoshot.Core.Tests` | `net8.0` | Core の単体テスト |
| `spikes/*` | 個別 | 使い捨ての技術 spike。`Annoshot.sln` に含めず CI の対象外。結論は各 `RESULT.md` に残す |

## ビルド

.NET 8 SDK が必要。

```sh
dotnet build Annoshot.sln
dotnet test Annoshot.sln
dotnet format Annoshot.sln --verify-no-changes
dotnet run --project src/Annoshot.App   # Windows のみ
```

## CI

`.github/workflows/ci.yml` が `main` への push と pull request で `windows-latest` 上の build / test / format check を実行する。
`dotnet format --verify-no-changes` の逸脱はジョブ失敗として扱う。ローカルで `dotnet format Annoshot.sln` を実行してから push すること。

## 既知の制約

- 開発ホストは WSL2 を想定している。`Directory.Build.props` の `EnableWindowsTargeting` により WSL 上でも Windows TFM を含む全プロジェクトの build / test は通るが、`Annoshot.App` の起動は Windows 上でのみ可能。
- 正式な検証は CI（`windows-latest`）で行う。
