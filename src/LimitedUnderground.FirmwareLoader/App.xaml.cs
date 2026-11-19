using System.Windows;

namespace LimitedUnderground.FirmwareLoader;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args is [LoaderMachineSetup.Argument])
        {
            Shutdown((int)LoaderMachineSetup.Install());
            return;
        }

        if (e.Args is [DailyLoaderWorker.Argument])
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(await DailyLoaderWorker.RunAsync());
            return;
        }

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
