using Microsoft.Win32;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LimitedUnderground.FirmwareLoader;

public partial class LoaderUpdatePanel : UserControl
{
    private readonly DispatcherTimer timer;
    public LoaderUpdatePanel()
    {
        InitializeComponent();
        Workflow = new LoaderUpdateWorkflow();
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background,
            (_, _) => Workflow.Poll(), Dispatcher);
        timer.Stop();
        Loaded += (_, _) => timer.Start();
        Unloaded += (_, _) => { timer.Stop(); Workflow.Invalidate(); };
    }
    internal LoaderUpdateWorkflow Workflow { get => (LoaderUpdateWorkflow)DataContext; set => DataContext = value; }
    internal void Invalidate() => Workflow.Invalidate();
    internal Task LastOperation { get; private set; } = Task.CompletedTask;
    private async void Start_Click(object sender, RoutedEventArgs e) { LastOperation = Workflow.StartAsync(); await LastOperation; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => Workflow.Cancel();
    private void Recovery_Click(object sender, RoutedEventArgs e) => Workflow.ReviewRecovery();

    internal static string LoaderVersion =>
        typeof(LoaderUpdatePanel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    // The user chooses where the file goes; its contents are fixed fields (LoaderUpdateWorkflow.SupportReport).
    private void Support_Click(object sender, RoutedEventArgs e)
    {
        if (!Workflow.CanSaveSupportFile) return;
        var now = DateTimeOffset.UtcNow;
        var dialog = new SaveFileDialog
        {
            Title = "Save a support file",
            FileName = $"loader-support-{now.UtcDateTime:yyyyMMdd-HHmmss}.txt",
            Filter = "Text files (*.txt)|*.txt",
            AddExtension = true,
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try { File.WriteAllText(dialog.FileName, Workflow.SupportReport(now, LoaderVersion)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(Window.GetWindow(this), "The support file could not be saved there. Choose another folder.",
                "Support file not saved", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
