namespace LimitedUnderground.FirmwareLoader;

// T-0068: nullable declarations keep unassigned production facts visibly unassigned.
internal enum D5ResetEffect { NoAdditionalMutations }
internal enum D5EraseEffect { OnlyDeclaredEraseRanges }
internal enum D5SpanRole { Written, Preserved }
internal sealed record D5SpanEvidence(HostPreservationRange Span, D5SpanRole Role,
    string OriginalSha256, string ExpectedReadbackSha256);
internal sealed record D5ObservedSpan(HostPreservationRange Span, string Sha256);
internal sealed record D5ContractDraft(string? ProfileId, string? InterfaceId, string? DriverId,
    HostSelection? Selection, HostPreservationPayloadBinding? Binding, D5ResetEffect? ResetEffect,
    D5EraseEffect? EraseEffect, ulong? EraseBlock, bool? RestoreEraseTails, bool? ObservableQuiescence,
    int? MaximumAttempts, HostAdapterProfile? Limits, IReadOnlyList<D5SpanEvidence>? Spans,
    string? OriginalSetDigest, string? OriginalBootImageDigest, string? WrittenBootImageDigest);

// Host-fixture consistency only. No operational token, physical profile or proof is issued.
internal sealed class D5FiniteWriterContract
{
    private readonly HostSelection selection;
    private readonly HostPreservationPayloadBinding binding;
    private readonly D5SpanEvidence[] spans;
    private readonly string originalSetDigest, originalBootDigest, writtenBootDigest;
    internal HostAdapterProfile Limits { get; }
    internal IReadOnlyList<D5SpanEvidence> Spans { get; }
    private D5FiniteWriterContract(D5ContractDraft draft, D5SpanEvidence[] spans)
    {
        selection=draft.Selection!;binding=draft.Binding!;Limits=draft.Limits!;this.spans=spans;
        Spans=Array.AsReadOnly(spans);originalSetDigest=draft.OriginalSetDigest!;
        originalBootDigest=draft.OriginalBootImageDigest!;writtenBootDigest=draft.WrittenBootImageDigest!;
    }
    internal static bool TryCreateHostFixture(D5ContractDraft? draft, out D5FiniteWriterContract? contract)
    {
        contract=null;
        if(draft is null || !Assigned(draft.ProfileId) || !Assigned(draft.InterfaceId) || !Assigned(draft.DriverId) ||
            draft.Selection is not {Identity:not null,Generation:>0} selected || !Assigned(selected.Target) ||
            !Assigned(selected.Model) || !Assigned(selected.Adapter) || draft.Binding is not {} binding ||
            draft.ResetEffect!=D5ResetEffect.NoAdditionalMutations || draft.EraseEffect!=D5EraseEffect.OnlyDeclaredEraseRanges ||
            draft.EraseBlock is null || draft.EraseBlock!=binding.Plan.EraseBlockSize ||
            draft.RestoreEraseTails is null || draft.ObservableQuiescence!=true || draft.MaximumAttempts!=1 ||
            draft.Limits is not {} limits || draft.Spans is null ||
            !Hash(draft.OriginalSetDigest) || !Hash(draft.OriginalBootImageDigest) || !Hash(draft.WrittenBootImageDigest)) return false;
        if(limits.WriteMilliseconds<=0 || limits.ReadMilliseconds<=0 || limits.BootMilliseconds<=0 ||
            limits.TotalMilliseconds<=0 || (long)limits.WriteMilliseconds+limits.ReadMilliseconds+limits.BootMilliseconds>limits.TotalMilliseconds ||
            limits.MaximumChunk<=0 || limits.MaximumChunk>65536 ||
            !limits.SideEffectsApproved || !limits.BootApproved || binding.Plan.AddressSpaceLength>uint.MaxValue ||
            binding.Plan.Writes.Count!=1 || binding.Payloads.Count!=1 || draft.WrittenBootImageDigest!=binding.Payloads[0].Sha256) return false;
        try
        {
            int count=draft.Spans.Count;
            if(count<=0 || count>HostPreservationPlan.MaximumSpans) return false;
            var copy=new D5SpanEvidence[count];
            for(int i=0;i<count;i++)
            {
                if(draft.Spans[i] is not {} item) return false;
                copy[i]=item;
            }
            Array.Sort(copy,(a,b)=>a.Span.Offset.CompareTo(b.Span.Offset));
            ulong cursor=0;int written=0;bool erasedTail=false;
            foreach(var item in copy)
            {
                var span=item.Span;
                if(span.Offset!=cursor || span.Length==0 || span.Length>binding.Plan.AddressSpaceLength-cursor ||
                    !Hash(item.OriginalSha256) || !Hash(item.ExpectedReadbackSha256)) return false;
                cursor=checked(cursor+span.Length);
                if(item.Role==D5SpanRole.Written)
                {
                    if(span!=binding.Plan.Writes[0] || item.ExpectedReadbackSha256!=binding.Payloads[0].Sha256) return false;
                    written++;
                }
                else if(item.Role==D5SpanRole.Preserved)
                {
                    if(item.OriginalSha256!=item.ExpectedReadbackSha256 || Overlaps(span,binding.Plan.Writes[0])) return false;
                    if(binding.Plan.EraseRanges.Any(e=>Overlaps(span,e))) erasedTail=true;
                }
                else return false;
            }
            if(cursor!=binding.Plan.AddressSpaceLength || written!=1 || erasedTail && draft.RestoreEraseTails!=true) return false;
            contract=new(draft,copy);return true;
        }
        catch(Exception) { return false; }
    }
    internal bool AllowsFixtureWrite(HostSelection? observed, ulong offset, ulong length, int attempt)
    {
        var write=binding.Plan.Writes[0];
        return SameSelection(observed) && attempt==1 && length>0 && length<=(ulong)Limits.MaximumChunk &&
            offset>=write.Offset && length<=write.Length && offset-write.Offset<=write.Length-length;
    }
    internal bool CompletionConsistent(HostUpdateState state, bool quiescent, string? originalBinding,
        IReadOnlyList<D5ObservedSpan>? compared, HostBootResult? boot, bool recovery=false)
    {
        if(state!=(recovery?HostUpdateState.Recovered:HostUpdateState.Verified) || !quiescent ||
            originalBinding!=originalSetDigest || compared is null || compared.Count!=spans.Length ||
            boot is null || !boot.Healthy || !boot.ProtectedStateMatches ||
            boot.ImageDigest!=(recovery?originalBootDigest:writtenBootDigest)) return false;
        // Complete evidence in canonical span order, including every gap and tail.
        for(int i=0;i<spans.Length;i++)
        {
            if(compared[i] is not {} actual || actual.Span!=spans[i].Span ||
                actual.Sha256!=(recovery?spans[i].OriginalSha256:spans[i].ExpectedReadbackSha256)) return false;
        }
        return true;
    }
    private bool SameSelection(HostSelection? current)=> current is not null && ReferenceEquals(current.Identity,selection.Identity) &&
        current.Generation==selection.Generation && current.Target==selection.Target && current.Model==selection.Model && current.Adapter==selection.Adapter;
    private static bool Assigned(string? value)=>!string.IsNullOrWhiteSpace(value) && value.Length<=128 && value==value.Trim() && !value.Any(char.IsControl) &&
        !value.Equals("UNASSIGNED",StringComparison.OrdinalIgnoreCase) && !value.Equals("UNKNOWN",StringComparison.OrdinalIgnoreCase);
    private static bool Hash(string? value)=>value is {Length:64} && value.All(c=>c is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static bool Overlaps(HostPreservationRange a,HostPreservationRange b)=>a.Offset<b.Offset+b.Length && b.Offset<a.Offset+a.Length;
}