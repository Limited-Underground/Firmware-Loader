namespace LimitedUnderground.FirmwareLoader;

// T-0056 opt-in only: no production provider/choice/adapter or root registration.
internal static class ProductionUpdateComposition
{
    internal static bool TryCreate(LoaderSessionController? controller, LoaderBundleInspectionContext? context,
        FirmwareBundleCandidateResult? inspection, Func<FirmwareBundleCandidateResult?>? currentInspection,
        ProductionAdmittedRelease? production, HostPreservationPayloadBinding? binding,
        Func<HostAdmissionPolicy?>? currentPolicy, Func<ProductionCurrentState?>? currentState,
        ProductionAdmissionState? clockState, IHostUpdateAdapter? adapter, HostOperationStore? operationStore,
        Func<long>? milliseconds, HostAdapterProfile? profile, out HostUpdateEngine? engine, bool recovery = false)
    {
        engine = null;
        if (production is null || binding is null || currentPolicy is null || currentState is null ||
            clockState is null || milliseconds is null || !ReferenceEquals(clockState, production.ClockState)) return false;
        // The verified class has a private constructor and retains the exact manifest,
        // host release, admission clock-store identity and observed compatibility snapshot.
        var manifest = production.Manifest;
        var release = production.HostRelease;
        var policy = release.Policy;
        if (manifest.HostBindings != release.Manifest) return false;

        HostAdmissionPolicy? LivePolicy()
        {
            try
            {
                var observedPolicy = currentPolicy();
                var observedState = currentState();
                long now = milliseconds();
                if (observedPolicy != policy || observedState != production.CurrentContext || observedState is null ||
                    now < 0 || now >= policy.ValidUntil ||
                    !ProductionReleaseAdmission.CurrentBindingsMatch(manifest, policy, observedState, now)) return null;
                // A valid old result never overrides current persisted time/revision floors.
                return clockState.TryAdvance(now, manifest.PolicyRevision) ? policy : null;
            }
            catch (Exception) { return null; }
        }

        try
        {
            // Refuse immutable payload/plan mismatch before any clock-state advancement.
            if (HostPreservationBindingPreflight.Evaluate(release, binding, policy, milliseconds()).Status !=
                HostPreservationBindingPreflightStatus.FixtureConsistent) return false;
            // The closure retains the entire typed production result for the attempt,
            // rather than discarding its additional signed metadata via .HostRelease.
            return HostUpdateComposition.TryCreate(controller, context, inspection, currentInspection, release, binding,
                policy, LivePolicy, adapter, operationStore, milliseconds, profile, out engine, recovery);
        }
        catch (Exception) { engine = null; return false; }
    }
}