using LimitedUnderground.FirmwareLoader;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;

// Exercises the client's two bounded waits and its failure references against an in-process named
// pipe. The real endpoint owner check and the scheduled task stay out of scope; both are injected.
internal static class DailyLoaderClientTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("daily worker client waits for a slow first reply after a quick endpoint", SlowFirstReply),
        ("daily worker client bounds the first reply", FirstReplyBounded),
        ("daily worker client bounds a missing endpoint and a refused task start", MissingEndpoint),
        ("daily worker client names a worker that closes before replying", ClosedBeforeReply),
        ("daily worker client names endpoint open and owner failures", EndpointFailures),
        ("daily worker client waits minutes for the cold runtime check", ProductionLimits),
        ("daily worker exit codes name each stage distinctly", WorkerStageCodes),
    };

    private static readonly QualificationOperationStatus Hello = new("Ready", "Choose the signed release.", "", false, false);

    private static void SlowFirstReply()
    {
        // Endpoint found at once, reply after 1.5 s: longer than the endpoint limit, inside the reply limit.
        // The former single deadline covered both and refused this shape once scaled.
        var result = Run(replyDelay: TimeSpan.FromMilliseconds(1500), closeInstead: false, endpointLimit: TimeSpan.FromMilliseconds(500),
            firstReplyLimit: TimeSpan.FromSeconds(10), out var elapsed);
        Require(result.Connected && result.Reference == "", $"slow first reply was refused ({result.Reference})");
        Require(elapsed >= TimeSpan.FromMilliseconds(1400), "reply arrived before the simulated runtime check");
    }

    private static void FirstReplyBounded()
    {
        var result = Run(replyDelay: TimeSpan.FromSeconds(5), closeInstead: false, endpointLimit: TimeSpan.FromSeconds(2),
            firstReplyLimit: TimeSpan.FromMilliseconds(600), out var elapsed);
        Require(!result.Connected && result.Reference == "R1", $"a reply after the limit was accepted ({result.Reference})");
        Require(elapsed < TimeSpan.FromSeconds(4), "the first-reply wait was not bounded");
    }

    private static void MissingEndpoint()
    {
        var timer = Stopwatch.StartNew();
        var none = DailyLoaderClient.ConnectAsync(() => 0, () => null, TimeSpan.FromMilliseconds(400),
            TimeSpan.FromSeconds(10), CancellationToken.None).GetAwaiter().GetResult();
        Require(none.Client is null && none.Reference == "E1" && timer.Elapsed < TimeSpan.FromSeconds(3), "a missing endpoint was not bounded");
        int attempts = 0;
        var refused = DailyLoaderClient.ConnectAsync(() => unchecked((int)0x80070005), () => { attempts++; return null; },
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), CancellationToken.None).GetAwaiter().GetResult();
        Require(refused.Client is null && refused.Reference == "T-80070005" && attempts == 0,
            $"a refused task start was not named or still looked for the endpoint ({refused.Reference})");
    }

    private static void ClosedBeforeReply()
    {
        var result = Run(replyDelay: TimeSpan.FromMilliseconds(200), closeInstead: true, endpointLimit: TimeSpan.FromSeconds(2),
            firstReplyLimit: TimeSpan.FromSeconds(10), out _);
        Require(!result.Connected && result.Reference == "R2", $"a worker refusal was not named ({result.Reference})");
    }

    private static void EndpointFailures()
    {
        var denied = DailyLoaderClient.ConnectAsync(() => 0, () => throw new Win32Exception(5), TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2), CancellationToken.None).GetAwaiter().GetResult();
        Require(denied.Client is null && denied.Reference == "E2-5", $"endpoint access failure not named ({denied.Reference})");
        var foreign = DailyLoaderClient.ConnectAsync(() => 0, () => throw new UnauthorizedAccessException("worker-endpoint"),
            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), CancellationToken.None).GetAwaiter().GetResult();
        Require(foreign.Client is null && foreign.Reference == "E3", $"foreign endpoint owner not named ({foreign.Reference})");
    }

    private static void ProductionLimits()
    {
        Require(DailyLoaderClient.EndpointLimit == TimeSpan.FromSeconds(30), "endpoint limit must match the worker's 30-second connect wait");
        Require(DailyLoaderClient.FirstReplyLimit >= TimeSpan.FromMinutes(2) && DailyLoaderClient.FirstReplyLimit <= TimeSpan.FromMinutes(5),
            "first-reply limit must cover a cold runtime check and stay bounded");
    }

    private static void WorkerStageCodes()
    {
        var codes = new HashSet<int>();
        foreach (DailyLoaderWorker.Stage stage in Enum.GetValues<DailyLoaderWorker.Stage>())
        {
            Require(codes.Add(DailyLoaderWorker.RefusalCode(stage)) && codes.Add(DailyLoaderWorker.ExceptionCode(stage)),
                $"stage {stage} shares an exit code");
        }
        Require(codes.All(code => code is > 10 and < 100), "worker exit codes must stay clear of 0-10 and fit in two digits");
        Require(DailyLoaderWorker.RefusalCode(DailyLoaderWorker.Stage.Runtime) == 18 &&
            DailyLoaderWorker.ExceptionCode(DailyLoaderWorker.Stage.Assignment) == 43, "documented worker codes changed");
    }

    private static (bool Connected, string Reference) Run(TimeSpan replyDelay, bool closeInstead, TimeSpan endpointLimit,
        TimeSpan firstReplyLimit, out TimeSpan elapsed)
    {
        string name = "LimitedUnderground.FirmwareLoader.Tests." + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var stop = new CancellationTokenSource();
        var worker = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(stop.Token);
            if (await DailyLoaderProtocol.ReadRequestAsync(server, stop.Token) != DailyLoaderRequest.Connect) return;
            await Task.Delay(replyDelay, stop.Token);
            if (closeInstead) { server.Disconnect(); return; }
            await DailyLoaderProtocol.WriteStatusAsync(server, Hello, stop.Token);
        });
        NamedPipeClientStream? Connect()
        {
            var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            pipe.Connect(2000);
            return pipe;
        }
        var timer = Stopwatch.StartNew();
        var result = DailyLoaderClient.ConnectAsync(() => 0, Connect, endpointLimit, firstReplyLimit, CancellationToken.None)
            .GetAwaiter().GetResult();
        elapsed = timer.Elapsed;
        stop.Cancel();
        try { worker.Wait(5000); } catch (AggregateException) { }
        if (result.Client is null) return (false, result.Reference);
        result.Client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return (true, result.Reference);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
