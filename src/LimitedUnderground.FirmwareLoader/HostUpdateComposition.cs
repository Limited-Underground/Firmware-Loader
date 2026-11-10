namespace LimitedUnderground.FirmwareLoader;

// Opt-in host composition only. Production registers neither this factory nor a device adapter.
internal static class HostUpdateComposition
{
    internal static bool TryCreate(LoaderSessionController? controller, LoaderBundleInspectionContext? context,
        FirmwareBundleCandidateResult? inspection, Func<FirmwareBundleCandidateResult?>? currentInspection,
        HostAdmittedRelease? release, HostPreservationPayloadBinding? binding, HostAdmissionPolicy? policy,
        Func<HostAdmissionPolicy?>? currentPolicy, IHostUpdateAdapter? adapter, HostOperationStore? store,
        Func<long>? milliseconds, HostAdapterProfile? profile, out HostUpdateEngine? engine, bool recovery = false)
    {
        engine = null;
        if (controller is null || context is null || inspection is null || currentInspection is null ||
            release is null || binding is null || policy is null || currentPolicy is null || adapter is null ||
            store is null || milliseconds is null || profile is null)
            return false;

        // Retain the immutable inputs, not a prior FixtureConsistent verdict. The existing
        // engine calls this guard again at selection, ownership and every command boundary.
        HostAdmissionPolicy? BoundPolicy()
        {
            try
            {
                var current = currentPolicy();
                if (current != policy) return null;
                var result = HostPreservationBindingPreflight.Evaluate(release, binding, current, milliseconds());
                return result.Status == HostPreservationBindingPreflightStatus.FixtureConsistent ? current : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        try
        {
            if (policy != release.Policy || !controller.CanPublishOfflineBundleInspection(context, inspection) ||
                !ReferenceEquals(inspection, currentInspection()) || BoundPolicy() is null)
                return false;

            // The same concrete store supplies exclusive ownership and the durable journal.
            // Construction does not select a device, confirm an attempt or acquire a lease.
            engine = new HostUpdateEngine(controller, context, inspection, currentInspection, release, policy,
                BoundPolicy, adapter, store, store, milliseconds, profile, recovery);
            return true;
        }
        catch (Exception)
        {
            engine = null;
            return false;
        }
    }
}