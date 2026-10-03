using LimitedUnderground.FirmwareLoader;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class LoaderUpdateUiTests
{
    internal static string RepositoryRoot = "";
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("UI production panel has no choices or operational authority", () => Sta(Production)),
        ("UI explicit selection plus separate confirmation required", () => Sta(Selection)),
        ("UI stale selection consumes old confirmation and cannot start", () => Sta(Reselect)),
        ("UI file product and close invalidation cancel actual engine", () => Sta(Invalidation)),
        ("UI cancellation retains uncertainty and ignores late completion", () => Sta(Cancel)),
        ("UI actual engine renders write readback boot and verified states", () => Sta(Progress)),
        ("UI failed independent readback never shows success", () => Sta(ReadFailure)),
        ("UI recovery requires separately admitted original and confirmation", () => Sta(Recovery)),
        ("UI private exception and identifier strings never enter visible projection", () => Sta(Privacy)),
        ("UI actual MainWindow hosts blocked panel and renders at minimum size", () => Sta(MainWindowIntegration)),
    };
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            try { action(); } catch (Exception e) { failure = e; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(15))) throw new TimeoutException("UI test STA did not finish");
        if (failure is not null) throw new InvalidOperationException("UI regression", failure);
    }
    private static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
    private static void Wait(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) { Drain(); Thread.Sleep(1); }
        Require(task.IsCompleted, "bounded UI completion"); task.GetAwaiter().GetResult(); Drain();
    }
    private static LoaderUpdateWorkflow Model(HostUpdateEngineTests.Fixture f, Func<bool>? failFactory = null) => new(new[] { f.Selection },
        (recovery, selected) => { if (failFactory?.Invoke() == true) throw new IOException("private factory detail"); f.Adapter.CurrentSelection = selected; return f.NewEngine(recovery: recovery); });
    private static LoaderUpdatePanel Panel(LoaderUpdateWorkflow model) => new() { Workflow = model };
    private static void Select(LoaderUpdatePanel panel)
    {
        Drain();
        var list = (ComboBox)panel.FindName("DeviceList"); list.SelectedIndex = 0; Drain();
        Require(panel.Workflow.SelectedChoice is not null, "actual ComboBox selection binds");
    }
    private static Task Start(LoaderUpdatePanel panel)
    {
        ((Button)panel.FindName("StartButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        return panel.LastOperation;
    }
    private static void Confirm(LoaderUpdatePanel panel)
    {
        ((CheckBox)panel.FindName("ConfirmationBox")).IsChecked = true; Drain();
        Require(panel.Workflow.Confirmed, "actual CheckBox confirmation binds");
    }
    private static void Render(FrameworkElement element, string name, int width = 650, int height = 570)
    {
        var root = new Border { Background = Brushes.LightGray, Padding = new Thickness(12), Child = element, Width = width, Height = height };
        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout(); Drain();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
        var folder = Path.Combine(RepositoryRoot, ".private", "LUF-0009c-ui"); Directory.CreateDirectory(folder);
        using var stream = File.Create(Path.Combine(folder, name + ".png"));
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream);
        root.Child = null;
    }
    private static void Production()
    {
        var panel = new LoaderUpdatePanel(); Drain();
        Require(panel.Workflow.Choices.Count == 0 && !panel.Workflow.CanStart && !panel.Workflow.CanChoose && !panel.Workflow.CanConfirm, "no production operation");
        Require(!((Button)panel.FindName("StartButton")).IsEnabled, "production actual button disabled");
        Render(panel, "01-production-blocked");
    }
    private static void Selection()
    {
        using var f = new HostUpdateEngineTests.Fixture(select: false); var m = Model(f); var p = Panel(m); Drain();
        Require(((ComboBox)p.FindName("DeviceList")).SelectedIndex == -1 && !m.CanStart, "no automatic selection");
        Render(p, "02-unselected"); Select(p); Require(m.CanConfirm && !m.CanStart, "selection is not confirmation");
        Require(m.ImageDigest.Contains(f.Manifest.ImageDigest, StringComparison.Ordinal) && m.Firmware.Contains("generation 1", StringComparison.Ordinal), "exact admitted firmware visible");
        Render(p, "03-preflight-ready"); Confirm(p); Require(m.CanStart, "explicit confirmation enables exact fixture attempt");
        Wait(Start(p)); Require(m.Heading == "Update verified" && f.Adapter.Writes == 1 && !m.CanStart, "one attempt only");
        Wait(Start(p)); Require(f.Adapter.Writes == 1, "old action cannot resubmit");
    }
    private static void Reselect()
    {
        using var f = new HostUpdateEngineTests.Fixture(select: false); var m = Model(f); var p = Panel(m); Select(p); Confirm(p);
        m.SelectedChoice = null; m.SelectedChoice = m.Choices[0];
        Require(!m.Confirmed && !m.CanStart, "selection change clears old confirmation"); Wait(Start(p)); Require(f.Adapter.Writes == 0, "stale action writes nothing");
    }
    private static void Invalidation()
    {
        foreach (bool inFlight in new[] { false, true })
        {
            using var f = new HostUpdateEngineTests.Fixture(select: false); var m = Model(f); var p = Panel(m); Select(p); Confirm(p);
            var pending = new TaskCompletionSource(); f.Adapter.Write = () => pending.Task;
            Task? running = inFlight ? Start(p) : null; m.Invalidate();
            Require(m.Choices.Count == 0 && m.SelectedChoice is null && !m.CanStart && !m.Confirmed, "all session changes clear authority");
            if (running is not null) { Wait(running); Require(m.Heading.Contains("unknown", StringComparison.Ordinal), "old uncertainty remains visible"); pending.SetResult(); Drain(); Require(m.Heading.Contains("unknown", StringComparison.Ordinal), "late success cannot populate new session"); }
            else Require(f.Adapter.Writes == 0, "invalidation before start submits nothing");
        }
    }
    private static void Cancel()
    {
        using var f = new HostUpdateEngineTests.Fixture(select: false); bool failFactory = false; var m = Model(f, () => failFactory); var p = Panel(m); Select(p); Confirm(p);
        var pending = new TaskCompletionSource(); f.Adapter.Write = () => pending.Task;
        var run = Start(p); ((Button)p.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait(run);
        Require(m.Heading.StartsWith("Outcome unknown", StringComparison.Ordinal) && !m.CanStart && f.Lease.Held, "cancelled pending command retains uncertainty/lease");
        Render(p, "07-uncertain"); pending.SetResult(); Drain();
        var choice = m.Choices[0];
        m.SelectedChoice = null;
        Require(m.Heading.Contains("unknown", StringComparison.Ordinal) && !m.CanStart, "deselect cannot hide prior uncertainty");
        failFactory = true; m.SelectedChoice = choice;
        Require(m.Heading.Contains("unknown", StringComparison.Ordinal) && !m.Detail.Contains("private factory detail", StringComparison.Ordinal), "failed new preflight preserves earlier uncertainty without private detail");
        m.SelectedChoice = null; failFactory = false; m.SelectedChoice = choice;
        Require(m.Heading.Contains("Recovery", StringComparison.Ordinal) || m.Heading.Contains("unknown", StringComparison.Ordinal), "reselection cannot hide prior uncertainty");
        Require(!m.CanStart && !m.Heading.Contains("verified", StringComparison.OrdinalIgnoreCase), "late completion cannot verify");
    }
    private static void Progress()
    {
        using var f = new HostUpdateEngineTests.Fixture(select: false); var m = Model(f); var p = Panel(m); Select(p); Confirm(p);
        var write = new TaskCompletionSource(); var read = new TaskCompletionSource<HostReadback>(); var boot = new TaskCompletionSource<HostBootResult>();
        f.Adapter.Write = () => write.Task; f.Adapter.Read = () => read.Task; f.Adapter.Boot = () => boot.Task;
        var run = Start(p); m.Refresh(); Require(m.Heading == "Writing update", "real engine writing"); Render(p, "04-writing");
        write.SetResult(); for(int i = 0; i < 10; i++) { Drain(); Thread.Sleep(1); } m.Refresh(); Require(m.Heading == "Written — verifying bytes", "ack not success"); Render(p, "05-readback");
        read.SetResult(new(f.Image, true)); for(int i = 0; i < 10; i++) { Drain(); Thread.Sleep(1); } m.Refresh(); Require(m.Heading == "Bytes verified — checking startup", "bytes not startup"); Render(p, "06-boot-wait");
        boot.SetResult(new(f.Manifest.ImageDigest, true, true)); Wait(run); Require(m.Heading == "Update verified", "full verification"); Render(p, "08-verified");
    }
    private static void ReadFailure()
    {
        using var f = new HostUpdateEngineTests.Fixture(select: false); var m = Model(f); var p = Panel(m); Select(p); Confirm(p);
        f.Adapter.Read = () => Task.FromResult(new HostReadback(new byte[] { 9 }, false)); Wait(Start(p));
        Require(m.Heading.StartsWith("Outcome unknown", StringComparison.Ordinal) && f.Adapter.Boots == 0, "bad bytes cannot boot or succeed");
    }
    private static void Recovery()
    {
        using var f = new HostUpdateEngineTests.Fixture(select: false); f.Journal.MarkUncertain(f.Manifest.ImageDigest);
        var m = Model(f); var p = Panel(m); Select(p); Require(m.CanReviewRecovery && !m.CanStart, "unresolved original requires review");
        ((Button)p.FindName("RecoveryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Require(m.CanConfirm && !m.CanStart && m.ActionLabel == "Restore reviewed original", "separate recovery confirmation");
        Render(p, "09-recovery-ready"); Confirm(p); Wait(Start(p));
        Require(m.Heading == "Original restored" && m.Detail.Contains("does not make", StringComparison.Ordinal), "recovery outcome distinct"); Render(p, "10-recovered");
    }
    private static void Privacy()
    {
        using var f = new HostUpdateEngineTests.Fixture(select: false);
        const string secret = "C:\\private\\sensitive USB-serial-SECRET COM99";
        var m = new LoaderUpdateWorkflow(new[] { f.Selection }, (_, _) => throw new IOException(secret)); var p = Panel(m); Select(p);
        Require(!m.Detail.Contains(secret, StringComparison.Ordinal) && !m.Selection.Contains("COM99", StringComparison.Ordinal), "sanitized factory failure");
        Require(m.Heading == "Preflight blocked" && !m.CanStart, "no operation after factory failure"); Render(p, "11-preflight-blocked");
    }
    private static void MainWindowIntegration()
    {
        var window = new MainWindow { Width = 720, Height = 520 };
        // Exercise the actual product choice routed handler, then render the actual window tree.
        var choices = (StackPanel)window.FindName("ProductChoicePanel");
        var trail = FindButtons(choices).Single(b => Equals(b.Tag, "opentrail"));
        trail.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Drain();
        var panel = (LoaderUpdatePanel)window.FindName("UpdatePanel");
        Require(panel.IsVisible == false || panel.Workflow.Choices.Count == 0, "main production no choices");
        Require(!panel.Workflow.CanStart && panel.Workflow.Heading == "Installation unavailable", "main invalidated disabled panel");
        var content = (FrameworkElement)window.Content; window.Content = null; Render(content, "12-main-window-top", 720, 520);
        var scroll = FindDescendants<ScrollViewer>(content).First(); scroll.ScrollToEnd(); Drain();
        Render(content, "13-main-window-bottom", 720, 520); window.Close();
    }
    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); if (child is T match) yield return match;
            foreach (var descendant in FindDescendants<T>(child)) yield return descendant;
        }
    }
    private static IEnumerable<Button> FindButtons(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); if (child is Button button) yield return button;
            foreach (var descendant in FindButtons(child)) yield return descendant;
        }
    }
}
