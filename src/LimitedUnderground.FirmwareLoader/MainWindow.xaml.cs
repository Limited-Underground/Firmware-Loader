using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace LimitedUnderground.FirmwareLoader;

public partial class MainWindow : Window
{
    private readonly LoaderSessionController session = new(
        LoaderProductionProviders.Factories,
        LoaderProductionProviders.SignerTrustPolicies);
    private CancellationTokenSource? dailyCancellation;

    public MainWindow()
    {
        InitializeComponent();
        Title = LoaderProductCatalog.WindowTitle;
        PublishSession();
    }

    protected override void OnClosed(EventArgs eventArgs)
    {
        dailyCancellation?.Cancel();
        ClearInspection();
        session.Dispose();
        base.OnClosed(eventArgs);
    }

    private void ProductChoiceButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = eventArgs;
        if (sender is not Button { Tag: string engineeringKey } ||
            !session.SelectProduct(engineeringKey))
        {
            return;
        }

        ClearInspection();
        PublishSession();
    }

    private void BackButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        ClearInspection();
        session.ReturnToProductChoice();
        PublishSession();
    }

    private void ChooseBundleButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        var dialog = new OpenFileDialog
        {
            Title = "Choose a firmware candidate to inspect",
            Filter = "Firmware candidate archives (*.zip)|*.zip|All files (*.*)|*.*",
            CheckFileExists = true,
            AddToRecent = false,
            Multiselect = false,
            ValidateNames = true,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        ClearInspection();
        OfflineBundleInspectionOutcome outcome;
        try
        {
            using var candidate = new FileStream(
                dialog.FileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            outcome = OfflineBundleInspectionWorkflow.Inspect(session, candidate);
        }
        catch (IOException)
        {
            outcome = OfflineBundleInspectionWorkflow.FileUnavailable();
        }
        catch (UnauthorizedAccessException)
        {
            outcome = OfflineBundleInspectionWorkflow.FileUnavailable();
        }

        PublishInspection(outcome);
    }

    private void PublishInspection(OfflineBundleInspectionOutcome outcome)
    {
        InspectionHeading.Text = outcome.Heading;
        InspectionSummary.Text = outcome.Summary;
        InspectionProduct.Text = $"Product: {outcome.ProductKey}";
        InspectionTarget.Text = $"Target: {outcome.TargetKey}";
        InspectionGeneration.Text = $"Release generation: {outcome.ReleaseGeneration}";
        InspectionImageBytes.Text = $"Image bytes: {outcome.ImageBytes}";
        InspectionStructure.Text = $"Structure: {outcome.StructureStatus}";
        InspectionDigest.Text = outcome.Published
            ? $"Image SHA-256: {outcome.ImageDigestStatus} — {outcome.ImageSha256}"
            : $"Image SHA-256: {outcome.ImageDigestStatus}";
        InspectionSignature.Text = $"Signature: {outcome.SignatureStatus}";
        InspectionTrust.Text = $"Signer trust: {outcome.SignerTrustStatus}";
        InspectionAdmission.Text = $"Installation admission: {outcome.AdmissionStatus}";
        InspectionResultPanel.Visibility = Visibility.Visible;
        InspectionHeading.Focus();
    }

    private const string PreviewFooter = "PREVIEW — INSPECTION ONLY. Firmware installation and recovery are not available.";
    private const string QualificationFooter = "QUALIFICATION INSTALL — board A only. This session can write firmware to the connected board after one confirmation.";

    private void ClearInspection()
    {
        SafetyFooter.Text = PreviewFooter;
        UpdatePanel.Invalidate();
        UpdatePanel.Visibility = Visibility.Collapsed;
        InspectionResultPanel.Visibility = Visibility.Collapsed;
        InspectionHeading.Text = string.Empty;
        InspectionSummary.Text = string.Empty;
        InspectionProduct.Text = string.Empty;
        InspectionTarget.Text = string.Empty;
        InspectionGeneration.Text = string.Empty;
        InspectionImageBytes.Text = string.Empty;
        InspectionStructure.Text = string.Empty;
        InspectionDigest.Text = string.Empty;
        InspectionSignature.Text = string.Empty;
        InspectionTrust.Text = string.Empty;
        InspectionAdmission.Text = string.Empty;
    }

    private void PublishSession()
    {
        var snapshot = session.Snapshot;
        ProductChoicePanel.Visibility = snapshot.HasProduct
            ? Visibility.Collapsed
            : Visibility.Visible;
        SelectedProductPanel.Visibility = snapshot.HasProduct
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (snapshot.Product is null)
        {
            SelectedProductName.Text = string.Empty;
            SelectedProductDescription.Text = string.Empty;
            SelectedProductStatus.Text = string.Empty;
            ContinueButton.IsEnabled = false;
            return;
        }

        SelectedProductName.Text = snapshot.Product.DisplayName;
        SelectedProductDescription.Text = snapshot.Product.Description;
        SelectedProductStatus.Text = snapshot.OfflineBundleInspectionAvailable
            ? "For qualification, connect only your assigned MeshTrail board and choose Install signed qualification release. Administrator rights are required for legacy board A sessions."
            : snapshot.Status;
        ContinueButton.IsEnabled = snapshot.OfflineBundleInspectionAvailable;
        InstallReleaseButton.IsEnabled = snapshot.OfflineBundleInspectionAvailable;
    }

    // L7 step 5 (D-063, D-065): the one qualification install path. It runs only with administrator
    // rights (the operation journal and board A pin are administrator-only) and only for board A.
    private async void InstallReleaseButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        ClearInspection();
        if (!MachineRecoveryStorePolicy.IsElevatedAdministrator() || MachineRecoveryStorePolicy.HasDailyState || DailyLoaderClient.HasRegisteredTask())
        {
            await RunDailyQualificationAsync();
            return;
        }
        var runtime = PackagedFlashRuntime.OpenInstalled();
        string helper = Path.Combine(AppContext.BaseDirectory, "flash-helper", "lu_flash_helper.py");
        if (runtime is null || !ProcessFlashHelperRunner.FileMatches(helper, BenchFlash.HelperSha256))
        {
            ShowInstallMessage("Flash tool unavailable", "The Firmware Loader package is incomplete or damaged. Ask for a verified copy of the package.");
            return;
        }
        try { _ = MachineRecoveryStorePolicy.Open(); }
        catch (Exception)
        {
            ShowInstallMessage("Recovery storage unavailable", "Run the Firmware Loader setup. If you already did, ask an administrator to check the recovery storage.");
            return;
        }
        var dialog = new OpenFolderDialog { Title = "Choose the signed release folder", Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;

        var pin = new QualificationBoardPin(QualificationBoardPin.RootPath);
        var install = runtime.CreateHelperInstall(helper);
        var board = BenchFlash.CreateBoard(new ProcessFlashHelperRunner(install));
        InstallReleaseButton.IsEnabled = false;
        ShowInstallMessage("Step 1 of 4: checking the release and the board", "Reading the connected board without changing it. This takes about a minute. Keep the board plugged in.");
        try
        {
            var prepared = await QualificationInstaller.PrepareAsync(session, dialog.FolderName, board, pin,
                MachineRecoveryStorePolicy.Open, BenchFlash.HelperSha256, install.PythonSha256, ReleaseTrustAnchor.PublicKeySpki(),
                () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                () => MessageBox.Show(this, "No board A is set on this PC yet. Set the connected board as board A? Only board A can install the qualification release, and this cannot be changed from the Loader later.",
                    "Set board A", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes,
                CancellationToken.None);
            ShowInstallMessage(prepared.Heading, prepared.Detail);
            if (prepared.Workflow is not null)
            {
                UpdatePanel.Workflow = prepared.Workflow;
                UpdatePanel.Visibility = Visibility.Visible;
                SafetyFooter.Text = QualificationFooter;
            }
        }
        catch (Exception)
        {
            ShowInstallMessage("Preparation stopped", "The board or the flash tool did not respond as expected during preparation. No update was started. Contact your test coordinator with this message and the time it happened.");
        }
        finally
        {
            InstallReleaseButton.IsEnabled = session.Snapshot.OfflineBundleInspectionAvailable;
        }
    }

    private async Task RunDailyQualificationAsync()
    {
        var dialog = new OpenFolderDialog { Title = "Choose the signed release folder", Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        var files = QualificationReleaseFiles.TryRead(dialog.FolderName);
        if (files is null)
        {
            ShowInstallMessage("Release folder not usable", "Choose the assigned signed release folder and keep the four release files together.");
            return;
        }

        using var lifetime = new CancellationTokenSource();
        dailyCancellation = lifetime;
        var cancellation = lifetime.Token;
        InstallReleaseButton.IsEnabled = ContinueButton.IsEnabled = BackButton.IsEnabled = false;
        SafetyFooter.Text = "QUALIFICATION INSTALL — approved MeshTrail boards only. A write requires your confirmation.";
        ShowInstallMessage("Checking the release and board", "Connect only the assigned board and keep it plugged in.");
        bool confirmationSent = false;
        try
        {
            await using var worker = await DailyLoaderClient.ConnectAsync(cancellation);
            if (worker is null)
            {
                ShowInstallMessage("Daily qualification unavailable", "Ask your test coordinator to check the setup and the Windows account assigned to this PC.");
                return;
            }
            var ready = await worker.PrepareAsync(files, cancellation);
            ShowInstallMessage(ready.Heading, ready.Detail);
            if (!ready.CanChooseBoard || ready.BoardLabel is null || MessageBox.Show(this,
                $"Choose {ready.BoardLabel} for this attempt. Make sure only the assigned board is connected.",
                "Choose the board", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) != MessageBoxResult.OK)
                return;

            var selected = await worker.SelectBoardAsync(cancellation);
            ShowInstallMessage(selected.Heading, selected.Detail);
            if (!selected.CanConfirm || MessageBox.Show(this,
                selected.ReleaseSummary + "\n\nConfirm this board and signed release for one attempt.",
                "Confirm firmware update", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;

            ShowInstallMessage("Updating the assigned board", "Keep the cable connected while the Loader writes, verifies the firmware and checks startup.");
            confirmationSent = true;
            var result = await worker.ConfirmAndRunAsync(cancellation);
            ShowInstallMessage(result.Heading, string.Join("\n", new[] { result.ReleaseSummary, result.Detail }
                .Where(text => !string.IsNullOrWhiteSpace(text))));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or
            TimeoutException or OperationCanceledException or System.Text.Json.JsonException)
        {
            ShowInstallMessage(confirmationSent ? "Outcome unknown" : "Daily qualification unavailable",
                confirmationSent
                    ? "Keep the board connected and contact your test coordinator before retrying or restoring."
                    : "Ask your test coordinator to check the approved MeshTrail setup.");
        }
        finally
        {
            dailyCancellation = null;
            if (!cancellation.IsCancellationRequested)
            {
                BackButton.IsEnabled = true;
                InstallReleaseButton.IsEnabled = ContinueButton.IsEnabled = session.Snapshot.OfflineBundleInspectionAvailable;
            }
        }
    }

    private void ShowInstallMessage(string heading, string detail)
    {
        InspectionHeading.Text = heading;
        InspectionSummary.Text = detail;
        InspectionResultPanel.Visibility = Visibility.Visible;
    }
}
