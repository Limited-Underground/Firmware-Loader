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
            (_, _) => { if (Workflow.IsBusy) Workflow.Refresh(); }, Dispatcher);
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
}
