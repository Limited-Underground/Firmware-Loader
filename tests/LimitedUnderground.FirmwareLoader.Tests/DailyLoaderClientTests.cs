using LimitedUnderground.FirmwareLoader;
using System.Diagnostics;
using System.IO.Pipes;

// Exercises the client's two bounded waits against an in-process named pipe. The real endpoint
// owner check and the scheduled task stay out of scope; both are injected here.
internal static class DailyLoaderClientTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("daily worker client waits for a slow first reply after a quick endpoint", SlowFirstReply),
        ("daily worker client bounds the first reply", FirstReplyBounded),
        ("daily worker client bounds a missing endpoint and a refused task start", MissingEndpoint),
        ("daily worker client waits minutes for the cold runtime check", ProductionLimits),
    };

    private static readonly QualificationOperationStatus Hello = new("Ready", "Choose the signed release.", "", false, false);

    private static void SlowFirstReply()
    {
        // Endpoint found at once, reply after 1.5 s: longer than the endpoint limit, inside the reply limit.
        // The former single 20-second deadline covered both and refused this shape once scaled.
        var client = Run(replyDelay: TimeSpan.FromMilliseconds(1500), endpointLimit: TimeSpan.FromMilliseconds(500),
            firstReplyLimit: TimeSpan.FromSeconds(10), out var elapsed);
        Require(client, "slow first reply was refused");
        Require(elapsed >= TimeSpan.FromMilliseconds(1400), "reply arrived before the simulated runtime check");
    }

    private static void FirstReplyBounded()
    {
        var client = Run(replyDelay: TimeSpan.FromSeconds(5), endpointLimit: TimeSpan.FromSeconds(2),
            firstReplyLimit: TimeSpan.FromMilliseconds(600), out var elapsed);
        Require(!client, "a reply after the limit was accepted");
        Require(elapsed < TimeSpan.FromSeconds(4), "the first-reply wait was not bounded");
    }

    private static void MissingEndpoint()
    {
        var timer = Stopwatch.StartNew();
        var none = DailyLoaderClient.ConnectAsync(() => true, () => null, TimeSpan.FromMilliseconds(400),
            TimeSpan.FromSeconds(10), CancellationToken.None).GetAwaiter().GetResult();
        Require(none is null && timer.Elapsed < TimeSpan.FromSeconds(3), "a missing endpoint was not bounded");
        int attempts = 0;
        var refused = DailyLoaderClient.ConnectAsync(() => false, () => { attempts++; return null; }, TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5), CancellationToken.None).GetAwaiter().GetResult();
        Require(refused is null && attempts == 0, "a refused task start still looked for the endpoint");
    }

    private static void ProductionLimits()
    {
        Require(DailyLoaderClient.EndpointLimit == TimeSpan.FromSeconds(30), "endpoint limit must match the worker's 30-second connect wait");
        Require(DailyLoaderClient.FirstReplyLimit >= TimeSpan.FromMinutes(2) && DailyLoaderClient.FirstReplyLimit <= TimeSpan.FromMinutes(5),
            "first-reply limit must cover a cold runtime check and stay bounded");
    }

    private static bool Run(TimeSpan replyDelay, TimeSpan endpointLimit, TimeSpan firstReplyLimit, out TimeSpan elapsed)
    {
        string name = "LimitedUnderground.FirmwareLoader.Tests." + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var stop = new CancellationTokenSource();
        var worker = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(stop.Token);
            if (await DailyLoaderProtocol.ReadRequestAsync(server, stop.Token) != DailyLoaderRequest.Connect) return;
            await Task.Delay(replyDelay, stop.Token);
            await DailyLoaderProtocol.WriteStatusAsync(server, Hello, stop.Token);
        });
        NamedPipeClientStream? Connect()
        {
            var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            pipe.Connect(2000);
            return pipe;
        }
        var timer = Stopwatch.StartNew();
        var client = DailyLoaderClient.ConnectAsync(() => true, Connect, endpointLimit, firstReplyLimit, CancellationToken.None)
            .GetAwaiter().GetResult();
        elapsed = timer.Elapsed;
        stop.Cancel();
        try { worker.Wait(5000); } catch (AggregateException) { }
        if (client is null) return false;
        client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return true;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
