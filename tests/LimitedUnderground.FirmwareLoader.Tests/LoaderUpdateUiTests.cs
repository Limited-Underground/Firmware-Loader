using LimitedUnderground.FirmwareLoader;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class LoaderUpdateUiTests
{
    internal static ScreenshotOutput? Output;
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
        ("UI support file has fixed fields only and no identifiers", () => Sta(SupportFile)),
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
    private static void Render(FrameworkElement element, string name, int width = 650, int height = 760)
    {
        var root = new Border { Background = Brushes.LightGray, Padding = new Thickness(12), Child = element, Width = width, Height = height };
        try
        {
            root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout(); Drain();
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
            using var stream = (Output ?? throw new InvalidOperationException("Screenshot output not admitted")).Open(name);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream);
        }
        finally { root.Child = null; }
    }
    private static string StepCodes(LoaderUpdateWorkflow m) => string.Concat(m.Steps.Select(s => s.Status switch
    {
        LoaderStepStatus.Done => 'D', LoaderStepStatus.Active => 'A', LoaderStepStatus.Failed => 'F',
        LoaderStepStatus.NotReached => '-', _ => 'w',
    }));
    private static void Production()
    {
        var panel = new LoaderUpdatePanel(); Drain();
        Require(panel.Workflow.Choices.Count == 0 && !panel.Workflow.CanStart && !panel.Workflow.CanChoose && !panel.Workflow.CanConfirm, "no production operation");
        Require(!((Button)panel.FindName("StartButton")).IsEnabled, "production actual button disabled");
        Require(StepCodes(panel.Workflow) == "wwww" && panel.Workflow.ProgressValue == 0 && !panel.Workflow.CanSaveSupportFile, "production: no steps reached");
        Render(panel, "01-production-blocked");
    }
    private static void Selection()
    {
        using var f = new HostUpdateEngineTests.Fixture(select: false); var m = Model(f); var p = Panel(m); Drain();
        Require(((ComboBox)p.FindName("DeviceList")).SelectedIndex == -1 && !m.CanStart, "no automatic selection");
        Render(p, "02-unselected"); Select(p); Require(m.CanConfirm && !m.CanStart, "selection is not confirmation");
        Require(m.ImageDigest.Contains(f.Manifest.ImageDigest, StringComparison.Ordinal) && m.Firmware.Contains("generation 1", StringComparison.Ordinal), "exact admitted firmware visible");
        Require(StepCodes(m) == "Dwww" && m.ProgressValue == 0.25 && m.NextStep.StartsWith("Tick", StringComparison.Ordinal), "preflight is step 1 done");
        Require(m.Summary.Contains("release generation 1", StringComparison.Ordinal), "plain summary names the release");
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
        Require(StepCodes(m) == "DF--" && m.CanSaveSupportFile, "cancelled write stops at step 2"); Render(p, "07-uncertain"); pending.SetResult(); Drain();
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
        var write = new TaskCompletionSource(); var read = new TaskCompletionSource<HostRegionCheck>(); var boot = new TaskCompletionSource<HostBootResult>();
        f.Adapter.Write = () => write.Task; f.Adapter.Read = () => read.Task; f.Adapter.Boot = () => boot.Task;
        var run = Start(p); m.Refresh(); Require(m.Heading == "Writing update", "real engine writing");
        Require(StepCodes(m) == "DAww" && m.NextStep.Contains("plugged in", StringComparison.Ordinal), "write step active"); Render(p, "04-writing");
        write.SetResult(); for(int i = 0; i < 10; i++) { Drain(); Thread.Sleep(1); } m.Refresh(); Require(m.Heading == "Written — verifying bytes", "ack not success");
        Require(StepCodes(m) == "DDAw" && m.ProgressValue == 0.5, "verify step active"); Render(p, "05-readback");
        read.SetResult(HostRegionCheck.Of(f.Image, true)); for(int i = 0; i < 10; i++) { Drain(); Thread.Sleep(1); } m.Refresh(); Require(m.Heading == "Bytes verified — checking startup", "bytes not startup");
        Require(StepCodes(m) == "DDDA", "start step active"); Render(p, "06-boot-wait");
        boot.SetResult(new(f.Manifest.ImageDigest, true, true)); Wait(run); Require(m.Heading == "Update verified", "full verification");
        Require(StepCodes(m) == "DDDD" && m.ProgressValue == 1 && m.NextStep.StartsWith("Done", StringComparison.Ordinal), "all steps done only when verified"); Render(p, "08-verified");
    }
    private static void ReadFailure()
    {
        using var f = new HostUpdateEngineTests.Fixture(select: false); var m = Model(f); var p = Panel(m); Select(p); Confirm(p);
        f.Adapter.Read = () => Task.FromResult(HostRegionCheck.Of(new byte[] { 9 }, false)); Wait(Start(p));
        Require(m.Heading.StartsWith("Outcome unknown", StringComparison.Ordinal) && f.Adapter.Boots == 0, "bad bytes cannot boot or succeed");
        Require(StepCodes(m) == "DDF-" && m.ProgressValue == 0.5, "failed byte check stops at step 3");
        Require(m.NextStep.StartsWith("Do not unplug", StringComparison.Ordinal) && m.CanSaveSupportFile, "unknown outcome: plain next step and support file");
    }
    private static void Recovery()
    {
        using var f = new HostUpdateEngineTests.Fixture(select: false); f.Journal.MarkUncertain(f.Manifest.ImageDigest);
        var m = Model(f); var p = Panel(m); Select(p); Require(m.CanReviewRecovery && !m.CanStart, "unresolved original requires review");
        ((Button)p.FindName("RecoveryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Require(m.CanConfirm && !m.CanStart && m.ActionLabel == "Restore reviewed original", "separate recovery confirmation");
        Render(p, "09-recovery-ready"); Confirm(p); Wait(Start(p));
        Require(m.Heading == "Original restored" && m.Detail.Contains("does not make", StringComparison.Ordinal), "recovery outcome distinct");
        Require(StepCodes(m) == "DDDD" && m.Summary.StartsWith("Put back", StringComparison.Ordinal) && m.NextStep.Contains("support file", StringComparison.Ordinal), "recovery steps and next step"); Render(p, "10-recovered");
    }
    private static void Privacy()
    {
        using var f = new HostUpdateEngineTests.Fixture(select: false);
        const string secret = "C:\\private\\sensitive USB-serial-SECRET COM99";
        var m = new LoaderUpdateWorkflow(new[] { f.Selection }, (_, _) => throw new IOException(secret)); var p = Panel(m); Select(p);
        Require(!m.Detail.Contains(secret, StringComparison.Ordinal) && !m.Selection.Contains("COM99", StringComparison.Ordinal), "sanitized factory failure");
        Require(m.Heading == "Preflight blocked" && !m.CanStart, "no operation after factory failure");
        Require(!m.Summary.Contains("COM99", StringComparison.Ordinal) && !m.NextStep.Contains("COM99", StringComparison.Ordinal), "plain lines carry no identifiers"); Render(p, "11-preflight-blocked");
    }
    private static void SupportFile()
    {
        using var f = new HostUpdateEngineTests.Fixture(select: false); var m = Model(f); var p = Panel(m); Select(p); Confirm(p);
        f.Adapter.Read = () => Task.FromResult(HostRegionCheck.Of(new byte[] { 9 }, false)); Wait(Start(p));
        var report = m.SupportReport(new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero), "test-version");
        foreach (var expected in new[] { "Created (UTC): 2026-11-13 12:00:00", "Loader version: test-version", "Session: simulated review",
                     "Steps: Check the board: done; Write the firmware: done; Verify the written bytes: stopped here; Check that it starts: not reached",
                     "Write started: yes", "Bytes verified: no", "Stop reason code: independent-readback", "Image SHA-256: " + f.Manifest.ImageDigest })
            Require(report.Contains(expected, StringComparison.Ordinal), "support file field: " + expected);
        // Only fixed labelled lines: every line is one of the known fields.
        var labels = new[] { "Limited Underground Firmware Loader - support file", "Created (UTC):", "Loader version:", "Session:", "Mode:", "Status:", "Detail:", "Steps:",
            "Earlier attempt unresolved:", "Engine state:", "Stop reason code:", "Binding check:", "Write started:", "Bytes verified:",
            "Safety journal kept its record:", "Release generation:", "Release compatibility:", "Image bytes:", "Image SHA-256:" };
        foreach (var line in report.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
            Require(labels.Any(l => line.StartsWith(l, StringComparison.Ordinal)), "unexpected support file line");
        Require(!report.Contains("COM", StringComparison.Ordinal) && !report.Contains(":\\", StringComparison.Ordinal) && !report.Contains(f.Selection.Identity.ToString() ?? "\0", StringComparison.Ordinal),
            "no port, path or device identity");
        // A factory failure carrying private text cannot reach the file.
        const string secret = "C:\\private\\sensitive USB-serial-SECRET COM99";
        var blocked = new LoaderUpdateWorkflow(new[] { f.Selection }, (_, _) => throw new IOException(secret)); var bp = Panel(blocked); Select(bp);
        var blockedReport = blocked.SupportReport(DateTimeOffset.UnixEpoch, "v");
        Require(!blockedReport.Contains("SECRET", StringComparison.Ordinal) && !blockedReport.Contains("COM99", StringComparison.Ordinal), "sanitized support file");
        Require(((Button)p.FindName("SupportButton")).IsEnabled, "support button enabled after an attempt");
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

// Cooperative single-writer guard, not an atomic defense against hostile directory replacement.
internal sealed class ScreenshotOutput
{
    private readonly string folder;
    private ScreenshotOutput(string folder) { this.folder = folder; }
    internal static void CheckAncestors(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Reparse output/source boundary refused");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
    internal static ScreenshotOutput Admit(string source, string output)
    {
        if (string.IsNullOrWhiteSpace(source) || !Path.IsPathFullyQualified(source) ||
            string.IsNullOrWhiteSpace(output) || !Path.IsPathFullyQualified(output))
            throw new IOException("Fully qualified source and output paths required");
        source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
        output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(output));
        CheckAncestors(source); CheckAncestors(output);
        if (!Directory.Exists(source)) throw new IOException("Source directory missing");
        var relative = Path.GetRelativePath(source, output);
        if (relative == "." || (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            throw new IOException("Output must be outside source tree");
        if (File.Exists(output) || Directory.Exists(output)) throw new IOException("Output already exists");
        var parent = Path.GetDirectoryName(output);
        if (parent is null || !Directory.Exists(parent)) throw new IOException("Output parent must exist");
        Directory.CreateDirectory(output);
        CheckAncestors(output);
        using (var marker = new FileStream(Path.Combine(output, ".loader-render-owner"), FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        return new ScreenshotOutput(output);
    }
    internal FileStream Open(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 ||
            name.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-')))
            throw new IOException("Invalid rendering name");
        CheckAncestors(folder);
        var path = Path.Combine(folder, name + ".png");
        CheckAncestors(path);
        return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    }
}
