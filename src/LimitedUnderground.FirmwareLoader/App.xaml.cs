using System.Windows;

namespace LimitedUnderground.FirmwareLoader;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args is [LoaderMachineSetup.Argument])
        {
            Shutdown((int)LoaderMachineSetup.Install());
            return;
        }

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
