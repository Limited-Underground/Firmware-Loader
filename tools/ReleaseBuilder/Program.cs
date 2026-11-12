using LimitedUnderground.FirmwareLoader;
using System.Globalization;
using System.Text;

// L7 step 2 release builder. Usage (PowerShell 7, from the Loader checkout):
//   dotnet run --project tools/ReleaseBuilder -- --candidate <Trail build dir> --current <Trail build dir>
//       --source-build-id <id> --out <new folder> [--generation 2] [--current-generation 1]
//       [--policy-revision 1] [--issued-at <UTC Unix ms>]
// It never reads a private key and never signs. Signing is a separate owner step (L7 step 3).
const string AppName = "opentrail_heltec_v4_bench.bin";
var options = new Dictionary<string, string>(StringComparer.Ordinal);
for (int i = 0; i + 1 < args.Length; i += 2)
{
    if (!args[i].StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(args[i][2..], args[i + 1]))
        return Fail("bad argument " + args[i]);
}
if (args.Length % 2 != 0) return Fail("every option needs a value");
string Need(string name) => options.TryGetValue(name, out var v) ? v : throw new ArgumentException("missing --" + name);
ulong Number(string name, ulong fallback) => options.TryGetValue(name, out var v) ? ulong.Parse(v, NumberStyles.None, CultureInfo.InvariantCulture) : fallback;

try
{
    string helperPath = Path.Combine(Directory.GetCurrentDirectory(), "tools", "flash-helper", "lu_flash_helper.py");
    if (!File.Exists(helperPath)) return Fail("run this from the Loader checkout (flash helper not found)");
    string candidate = Path.GetFullPath(Need("candidate")), current = Path.GetFullPath(Need("current"));
    string output = Path.GetFullPath(Need("out"));
    if (Directory.Exists(output) || File.Exists(output)) return Fail("output folder already exists: " + output);
    long issued = (long)Number("issued-at", (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    var input = new QualificationInputs(
        File.ReadAllBytes(Path.Combine(candidate, AppName)),
        File.ReadAllBytes(Path.Combine(current, AppName)),
        File.ReadAllBytes(Path.Combine(current, "bootloader", "bootloader.bin")),
        Need("source-build-id"), Number("generation", 2), Number("current-generation", 1), Number("policy-revision", 1), issued,
        HostReleaseAdmission.Digest(File.ReadAllBytes(helperPath)));
    var release = QualificationReleaseBuilder.Build(input);

    Directory.CreateDirectory(output);
    void Save(string name, byte[] bytes) => File.WriteAllBytes(Path.Combine(output, name), bytes);
    Save("release.json", release.Manifest);
    Save("raw.bin", release.RawImage);
    Save("padded.bin", release.PaddedImage);
    Save("writer-profile.json", release.WriterProfile);
    Save("preservation-plan.json", release.PreservationPlan);
    Save("recovery-requirements.json", release.RecoveryRequirements);
    Save("recovery-image.bin", release.RecoveryImage);

    ProductionReleaseManifest.TryParse(release.Manifest, out var m);
    var p = release.Policy;
    string Time(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
    var summary = new StringBuilder()
        .AppendLine("QUALIFICATION RELEASE - board A only, test use only (D-063). Read before signing.")
        .AppendLine()
        .AppendLine($"Firmware:        Trail heltec_v4_bench, source {m!.Text("sourceBuildId")}")
        .AppendLine($"Image:           {m.Number("rawImageLength"):N0} bytes, padded to {m.Number("paddedImageLength"):N0}, written at 0x10000")
        .AppendLine($"Image SHA-256:   {m.Text("paddedImageSha256")} (padded)")
        .AppendLine($"Generation:      {m.Number("generation")} (board must be at generation {m.Number("maximumCurrentGeneration")})")
        .AppendLine($"Board must have: bootloader {m.Text("requiredBootloader")}")
        .AppendLine($"                 app        {m.Text("requiredCurrentFirmware")}")
        .AppendLine($"Restore image:   {m.Text("recoveryImageDigest")} (what is on the board now)")
        .AppendLine($"Signed by:       key {m.Text("publicKeyFingerprint")}")
        .AppendLine($"Valid:           {Time(m.IssuedAt)} to {Time(m.ExpiresAt)} (180 days), policy revision {m.PolicyRevision}")
        .AppendLine($"Writer profile:  {m.Text("writerProfileDigest")}")
        .AppendLine($"Layout rule:     {m.Text("preservationPlanDigest")}")
        .AppendLine($"Recovery rule:   {m.Text("recoveryRequirementsDigest")}")
        .AppendLine()
        .AppendLine($"Loader policy for this release: allowed 0x{p.AllowedOffset:X}+0x{p.AllowedLength:X}, protected 0x{p.ProtectedOffset:X}+0x{p.ProtectedLength:X}, minimum generation {p.MinimumGeneration}.")
        .AppendLine()
        .AppendLine("Sign release.json exactly as it is (L7 step 3). Any change after signing makes the Loader refuse it.");
    Save("SUMMARY.txt", Encoding.UTF8.GetBytes(summary.ToString()));
    Console.WriteLine(summary.ToString());
    Console.WriteLine("Release folder: " + output);
    return 0;
}
catch (Exception error) { return Fail(error.Message); }

static int Fail(string message)
{
    Console.Error.WriteLine("STOPPED: " + message);
    return 1;
}
