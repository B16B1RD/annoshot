using Annoshot.Core;
using Avalonia.Controls;

namespace Annoshot.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = AppInfo.Name;
    }
}
