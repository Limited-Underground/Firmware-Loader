using System.IO;

namespace LimitedUnderground.FirmwareLoader;

internal enum LoaderSetupResult
{
    Ready = 0,
    NeedsAdministrator = 20,
    RuntimeUnavailable = 21,
    StoreUnavailable = 22
}

internal static class LoaderMachineSetup
{
    internal const string Argument = "--setup-machine-store";

    internal static LoaderSetupResult Install() => Install(
        MachineRecoveryStorePolicy.IsElevatedAdministrator,
        () =>
        {
            var runtime = PackagedFlashRuntime.OpenInstalled();
            string helper = Path.Combine(AppContext.BaseDirectory, "flash-helper", "lu_flash_helper.py");
            return runtime is not null && ProcessFlashHelperRunner.FileMatches(helper, BenchFlash.HelperSha256);
        },
        () =>
        {
            // A valid existing store is reusable. Enroll refuses a partial or conflicting store.
            try { _ = MachineRecoveryStorePolicy.Open(); }
            catch (Exception) { _ = MachineRecoveryStorePolicy.Enroll(); }
        });

    internal static LoaderSetupResult Install(Func<bool> isAdministrator, Func<bool> verifyRuntime, Action prepareStore)
    {
        if (!isAdministrator()) return LoaderSetupResult.NeedsAdministrator;
        if (!verifyRuntime()) return LoaderSetupResult.RuntimeUnavailable;
        try
        {
            prepareStore();
            return LoaderSetupResult.Ready;
        }
        catch (Exception)
        {
            return LoaderSetupResult.StoreUnavailable;
        }
    }
}
