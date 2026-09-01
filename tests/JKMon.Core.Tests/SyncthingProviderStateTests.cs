using System.Net;
using System.Net.Http;
using System.Text;
using JKMon.Core.Sync;

namespace JKMon.Core.Tests;

/// <summary>Drives the real provider against a stubbed REST API so folder-state handling is checked end to end.</summary>
public class SyncthingProviderStateTests
{
    private const string Ok = "{}";
    private const string CleanCompletion = """{"completion":100,"needBytes":0,"needItems":0,"needDeletes":0}""";
    private const string NoConnections = """{"connections":{}}""";
    private const string PrimingEvent = """[{"id":1,"type":"Starting","data":{}}]""";
    private const string NoEvents = "[]";

    private static string Folder(bool paused = false) =>
        $$$"""[{"id":"f1","label":"Docs","paused":{{{(paused ? "true" : "false")}}}}]""";

    private static string Status(string state, long need = 0, long pullErrors = 0) =>
        $$$"""{"state":"{{{state}}}","needTotalItems":{{{need}}},"pullErrors":{{{pullErrors}}},"receiveOnlyChangedFiles":0}""";

    private static string StateChangedTo(string state) =>
        $$$"""[{"id":2,"type":"StateChanged","data":{"folder":"f1","from":"idle","to":"{{{state}}}"}}]""";

    [Fact]
    public async Task IdleFolderWithNothingOutstandingIsGreen()
    {
        var provider = Build(Route(Status("idle")));

        var snapshot = await provider.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(SyncState.UpToDate, snapshot.State);
        Assert.Equal("all folders up to date", snapshot.Detail);
    }

    [Theory]
    [InlineData("scanning")]
    [InlineData("syncing")]
    [InlineData("sync-preparing")]
    [InlineData("cleaning")]
    public async Task NonIdleFolderIsRedEvenWhenCompletionIsFull(string state)
    {
        var provider = Build(Route(Status(state)));

        var snapshot = await provider.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(SyncState.Synchronizing, snapshot.State);
        Assert.Equal($"Docs {state}", snapshot.Detail);
    }

    [Fact]
    public async Task PausedFolderIsRed()
    {
        var provider = Build(Route(Status("idle"), folders: Folder(paused: true)));

        var snapshot = await provider.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(SyncState.Synchronizing, snapshot.State);
        Assert.Equal("Docs paused", snapshot.Detail);
    }

    [Fact]
    public async Task PullErrorsReportAnError()
    {
        var provider = Build(Route(Status("idle", pullErrors: 3)));

        var snapshot = await provider.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(SyncState.Error, snapshot.State);
        Assert.Equal("Docs 3 pull error(s)", snapshot.Detail);
    }

    [Fact]
    public async Task OutOfSyncItemsAreRedWhileTheFolderSitsIdle()
    {
        var provider = Build(Route(Status("idle", need: 7)));

        var snapshot = await provider.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(SyncState.Synchronizing, snapshot.State);
        Assert.Equal("Docs 7 item(s) out of sync", snapshot.Detail);
    }

    /// <summary>
    /// The key regression: a folder that entered a non-idle state must stay red long after the short activity
    /// window has expired, rather than falling back to green because no further events arrived.
    /// </summary>
    [Fact]
    public async Task StateFromAnEventOutlivesTheActivityWindow()
    {
        var polls = 0;
        var time = new StubTime(DateTimeOffset.UnixEpoch);

        // db/status only ever reports idle, so a red result can only have come from the event.
        var provider = Build(path =>
        {
            if (path.StartsWith("/rest/events", StringComparison.Ordinal))
            {
                return polls switch
                {
                    1 => PrimingEvent,
                    2 => StateChangedTo("scanning"),
                    _ => NoEvents
                };
            }

            return Route(Status("idle"))(path);
        }, time);

        polls = 1;
        Assert.Equal(SyncState.UpToDate, (await provider.GetSnapshotAsync(CancellationToken.None)).State);

        polls = 2;
        Assert.Equal(SyncState.Synchronizing, (await provider.GetSnapshotAsync(CancellationToken.None)).State);

        polls = 3;
        time.Advance(TimeSpan.FromMinutes(5));
        var settled = await provider.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(SyncState.Synchronizing, settled.State);
        Assert.Equal("Docs scanning", settled.Detail);
    }

    [Fact]
    public async Task ReturningToIdleGoesGreenAgain()
    {
        var polls = 0;
        var time = new StubTime(DateTimeOffset.UnixEpoch);

        var provider = Build(path =>
        {
            if (path.StartsWith("/rest/events", StringComparison.Ordinal))
            {
                return polls switch
                {
                    1 => PrimingEvent,
                    2 => StateChangedTo("scanning"),
                    3 => StateChangedTo("idle"),
                    _ => NoEvents
                };
            }

            return Route(Status("idle"))(path);
        }, time);

        polls = 1;
        await provider.GetSnapshotAsync(CancellationToken.None);
        polls = 2;
        await provider.GetSnapshotAsync(CancellationToken.None);
        polls = 3;
        await provider.GetSnapshotAsync(CancellationToken.None);

        polls = 4;
        time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(SyncState.UpToDate, (await provider.GetSnapshotAsync(CancellationToken.None)).State);
    }

    /// <summary>db/status is documented as expensive, so it must only seed a folder rather than run every poll.</summary>
    [Fact]
    public async Task TheExpensiveStatusCallSeedsAFolderOnlyOnce()
    {
        var statusCalls = 0;
        var provider = Build(path =>
        {
            if (path.StartsWith("/rest/db/status", StringComparison.Ordinal))
            {
                statusCalls++;
            }

            return Route(Status("idle"))(path);
        });

        for (var i = 0; i < 5; i++)
        {
            await provider.GetSnapshotAsync(CancellationToken.None);
        }

        Assert.Equal(1, statusCalls);
    }

    private static Func<string, string?> Route(string status, string? folders = null) => path =>    {
        if (path.StartsWith("/rest/db/status", StringComparison.Ordinal))
        {
            return status;
        }

        if (path.StartsWith("/rest/events", StringComparison.Ordinal))
        {
            return NoEvents;
        }

        return path switch
        {
            "/rest/noauth/health" => Ok,
            "/rest/db/completion" => CleanCompletion,
            "/rest/system/connections" => NoConnections,
            "/rest/config/folders" => folders ?? Folder(),
            _ => null
        };
    };

    private static SyncthingSyncProvider Build(Func<string, string?> route, TimeProvider? time = null) =>
        new(
            () => new SyncthingEndpoint { BaseAddress = new Uri("http://127.0.0.1:8384"), ApiKey = "test-key" },
            new HttpClient(new StubHandler(route)),
            time ?? new StubTime(DateTimeOffset.UnixEpoch));

    private sealed class StubHandler(Func<string, string?> route) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = route(request.RequestUri!.PathAndQuery);

            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
        }
    }

    private sealed class StubTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        internal void Advance(TimeSpan amount) => _now += amount;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
