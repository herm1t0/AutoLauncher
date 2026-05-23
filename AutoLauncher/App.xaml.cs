using System.Windows;
using Shared;

namespace AutoLauncher;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Handle Windows shutdown / logoff -- give our windows a chance to clean up
        SessionEnding += OnSessionEnding;
    }

    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        Logger.Info("Session ending, shutting down...");

        // Signal all windows to close gracefully
        foreach (Window window in Windows)
        {
            if (window is MainWindow)
                window.Close();
        }
    }
}
