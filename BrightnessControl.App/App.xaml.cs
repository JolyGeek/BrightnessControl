using System.Windows;

namespace BrightnessControl;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var launchAtSignIn = e.Args.Contains("--startup", StringComparer.OrdinalIgnoreCase);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        if (launchAtSignIn)
            window.Hide();
    }
}
