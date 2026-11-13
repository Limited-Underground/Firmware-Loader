using Microsoft.Win32;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;

namespace LimitedUnderground.FirmwareLoader;

public partial class MainWindow : Window
{
    private readonly LoaderSessionController session = new(
        LoaderProductionProviders.Factories,
        LoaderProductionProviders.SignerTrustPolicies);

    public MainWindow()
    {
        InitializeComponent();
        Title = LoaderProductCatalog.WindowTitle;
        PublishSession();
    }

    protected override void OnClosed(EventArgs eventArgs)
    {
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

    private void ClearInspection()
    {
        UpdatePanel.Invalidate();
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
        SelectedProductStatus.Text = snapshot.Status;
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
        if (!MachineRecoveryStorePolicy.IsElevatedAdministrator())
        {
            ShowInstallMessage("Run the Loader as administrator", "Installing the qualification release needs administrator rights on this PC. Close the Loader, right-click it and choose Run as administrator.");
            return;
        }
        var dialog = new OpenFolderDialog { Title = "Choose the signed release folder", Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;

        var pin = new QualificationBoardPin(QualificationBoardPin.RootPath);
        string python = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".espressif", "python_env", "idf6.0_py3.14_env", "Scripts", "python.exe");
        string helper = Path.Combine(AppContext.BaseDirectory, "flash-helper", "lu_flash_helper.py");
        string? pythonSha = pin.PythonSha256();
        try { pythonSha ??= File.Exists(python) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(python))) : null; }
        catch (Exception) { pythonSha = null; }
        if (pythonSha is null)
        {
            ShowInstallMessage("Flash tool not found", "The ESP-IDF Python environment was not found on this PC. Nothing was written.");
            return;
        }
        var board = BenchFlash.CreateBoard(new ProcessFlashHelperRunner(new FlashHelperInstall(python, pythonSha, helper, BenchFlash.HelperSha256)));
        InstallReleaseButton.IsEnabled = false;
        ShowInstallMessage("Checking the release and the board", "Reading the connected board (read-only). This takes about a minute.");
        try
        {
            var prepared = await QualificationInstaller.PrepareAsync(session, dialog.FolderName, board, pin,
                MachineRecoveryStorePolicy.Open, BenchFlash.HelperSha256, pythonSha, ReleaseTrustAnchor.PublicKeySpki(),
                () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                () => MessageBox.Show(this, "No board A is set on this PC yet. Set the connected board as board A? Only board A can install the qualification release, and this cannot be changed from the Loader later.",
                    "Set board A", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes,
                CancellationToken.None);
            ShowInstallMessage(prepared.Heading, prepared.Detail);
            if (prepared.Workflow is not null) UpdatePanel.Workflow = prepared.Workflow;
        }
        catch (Exception)
        {
            ShowInstallMessage("Preparation stopped", "The board or the flash tool did not respond as expected. Nothing was written.");
        }
        finally
        {
            InstallReleaseButton.IsEnabled = session.Snapshot.OfflineBundleInspectionAvailable;
        }
    }

    private void ShowInstallMessage(string heading, string detail)
    {
        InspectionHeading.Text = heading;
        InspectionSummary.Text = detail;
        InspectionResultPanel.Visibility = Visibility.Visible;
    }
}
