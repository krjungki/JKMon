using System.Net;
using System.Text;
using JKMon.Core.Settings;
using JKMon.Core.Update;

namespace JKMon.Core.Tests;

public class UpdateCheckTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A run that began a moment ago, which is the case the start-up switch is about.</summary>
    private static readonly DateTimeOffset JustStarted = Now.AddSeconds(-3);

    [Fact]
    public void Schedule_NeverBlocksEveryAutomaticCheck()
    {
        Assert.False(UpdateSchedule.IsDue(UpdateCheckFrequency.Never, default, JustStarted, Now, true, false));
        Assert.False(UpdateSchedule.IsDue(UpdateCheckFrequency.Never, default, JustStarted, Now, false, false));
    }

    /// <summary>
    /// The switch says the app checks when it starts, so it has to mean that literally. Obeying the interval here
    /// made a restart do nothing when a check had run minutes earlier, which is exactly what a user reported.
    /// </summary>
    [Fact]
    public void Schedule_StartupCheckIgnoresTheInterval()
    {
        var minutesAgo = Now.AddMinutes(-24);

        Assert.True(UpdateSchedule.IsDue(
            UpdateCheckFrequency.Daily, minutesAgo, JustStarted, Now, checkAtStartup: true, alreadyCheckedThisRun: false));
    }

    [Fact]
    public void Schedule_StartupCheckHappensOnlyOncePerRun()
    {
        var minutesAgo = Now.AddMinutes(-24);

        Assert.False(UpdateSchedule.IsDue(
            UpdateCheckFrequency.Daily, minutesAgo, JustStarted, Now, checkAtStartup: true, alreadyCheckedThisRun: true));
    }

    [Fact]
    public void Schedule_AfterTheStartupCheckTheIntervalTakesOver()
    {
        var dayAgo = Now.AddHours(-25);

        Assert.True(UpdateSchedule.IsDue(
            UpdateCheckFrequency.Daily, dayAgo, JustStarted, Now, checkAtStartup: true, alreadyCheckedThisRun: true));
    }

    /// <summary>
    /// The refresh loop used to ask with a different argument than the start-up path, so a launch triggered a check
    /// seconds later even with the switch off. There is one decision now, and this pins it.
    /// </summary>
    [Fact]
    public void Schedule_LaunchingDoesNotCheckWhenTheStartupSwitchIsOff()
    {
        var longAgo = Now.AddDays(-30);

        Assert.False(UpdateSchedule.IsDue(UpdateCheckFrequency.Daily, longAgo, JustStarted, Now, false, false));
    }

    [Fact]
    public void Schedule_WithTheSwitchOffItChecksAfterAFullIntervalOfRunning()
    {
        var started = Now.AddHours(-25);
        var lastCheck = Now.AddDays(-30);

        Assert.True(UpdateSchedule.IsDue(UpdateCheckFrequency.Daily, lastCheck, started, Now, false, false));
    }

    [Fact]
    public void Schedule_WithTheSwitchOffAFreshInstallStillWaits()
    {
        Assert.False(UpdateSchedule.IsDue(UpdateCheckFrequency.Weekly, default, JustStarted, Now, false, false));
    }

    [Fact]
    public void Schedule_FirstCheckIsDueAtStartupWhenTheSwitchIsOn()
    {
        Assert.True(UpdateSchedule.IsDue(UpdateCheckFrequency.Weekly, default, JustStarted, Now, true, false));
    }

    [Theory]
    [InlineData(UpdateCheckFrequency.Daily, 23, false)]
    [InlineData(UpdateCheckFrequency.Daily, 24, true)]
    [InlineData(UpdateCheckFrequency.Weekly, 167, false)]
    [InlineData(UpdateCheckFrequency.Weekly, 168, true)]
    public void Schedule_WaitsForTheInterval(UpdateCheckFrequency frequency, int hoursSince, bool expected)
    {
        var last = Now.AddHours(-hoursSince);
        var started = Now.AddDays(-60);

        Assert.Equal(expected, UpdateSchedule.IsDue(frequency, last, started, Now, false, true));
        Assert.Equal(expected, UpdateSchedule.IsDue(frequency, last, started, Now, true, true));
    }

    [Fact]
    public void Schedule_RecoversFromAClockThatMovedBackwards()
    {
        var last = Now.AddDays(5);
        var started = Now.AddDays(-60);

        Assert.True(UpdateSchedule.IsDue(UpdateCheckFrequency.Daily, last, started, Now, false, true));
    }

    [Fact]
    public void Schedule_FailureBackoffIsShorterThanEveryInterval()
    {
        Assert.True(UpdateSchedule.RetryAfterFailure < UpdateSchedule.IntervalOf(UpdateCheckFrequency.Daily));
        Assert.True(UpdateSchedule.RetryAfterFailure > TimeSpan.Zero);
    }

    [Fact]
    public void Settings_DefaultToNoAutomaticChecking()
    {
        var settings = new JkMonSettings().Normalized();

        Assert.Equal(UpdateCheckFrequency.Never, settings.UpdateCheck);
        Assert.False(settings.CheckUpdatesOnStartup);
        Assert.Equal(default, settings.LastUpdateCheckUtc);
    }

    [Fact]
    public void Settings_RepairAnUndefinedFrequency()
    {
        var settings = new JkMonSettings { UpdateCheck = (UpdateCheckFrequency)42 }.Normalized();

        Assert.Equal(UpdateCheckFrequency.Never, settings.UpdateCheck);
    }

    [Fact]
    public void Checksums_ParseOnlyWellFormedLines()
    {
        var content = string.Join('\n',
            "SHA-256 checksums for JKMon 0.1.0.",
            string.Empty,
            "47051CA48D90F0102CAD87DEC5AFA665956B9588CD7FF43AD847550D0E347900  JKMon-0.1.0-win-x64.zip",
            "5FEA10DE3CB464B3743FF658987425E7299C906FFB01A9A0B9BB4D570A8ED620  JKMon.exe",
            "not-a-hash  ignored.txt");

        var parsed = ReleaseChecksums.Parse(content);

        Assert.Equal(2, parsed.Count);
        Assert.Equal("5FEA10DE3CB464B3743FF658987425E7299C906FFB01A9A0B9BB4D570A8ED620", parsed["JKMon.exe"]);
        Assert.DoesNotContain("ignored.txt", parsed.Keys);
    }

    [Fact]
    public void Checksums_CompareWithoutCaringAboutCase()
    {
        using var stream = new MemoryStream("jkmon"u8.ToArray());
        var hash = ReleaseChecksums.HashOf(stream);

        Assert.True(ReleaseChecksums.Matches(hash.ToLowerInvariant(), hash));
        Assert.False(ReleaseChecksums.Matches(new string('0', 64), hash));
        Assert.False(ReleaseChecksums.Matches("short", hash));
    }

    [Fact]
    public async Task Checker_ReadsTheVersionAndBuildsAssetUrls()
    {
        using var http = Stub(HttpStatusCode.OK, """{ "version": "0.2.0" }""");
        var info = await new UpdateChecker(http, "owner/repo").TryGetLatestAsync(CancellationToken.None);

        Assert.NotNull(info);
        Assert.Equal("0.2.0", info!.Value.Version.ToString());
        Assert.Equal("JKMon-0.2.0-win-x64.zip", info.Value.PackageName);
        Assert.Equal(
            "https://github.com/owner/repo/releases/latest/download/JKMon-0.2.0-win-x64.zip",
            info.Value.PackageUrl.ToString());
        Assert.EndsWith("/SHA256SUMS.txt", info.Value.ChecksumUrl.ToString());
    }

    [Fact]
    public async Task Checker_TreatsAMissingReleaseAsUnknownRatherThanUpToDate()
    {
        using var http = Stub(HttpStatusCode.NotFound, string.Empty);

        Assert.Null(await new UpdateChecker(http, "owner/repo").TryGetLatestAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "version": "nonsense" }""")]
    [InlineData("not json")]
    public async Task Checker_RejectsAnUnreadableVersionFile(string body)
    {
        using var http = Stub(HttpStatusCode.OK, body);

        Assert.Null(await new UpdateChecker(http, "owner/repo").TryGetLatestAsync(CancellationToken.None));
    }

    [Fact]
    public void Checker_TargetsTheConfiguredRepositoryOnly()
    {
        using var http = Stub(HttpStatusCode.OK, "{}");
        var checker = new UpdateChecker(http, "owner/repo");

        Assert.Equal("https://github.com/owner/repo/releases/latest/download/", checker.LatestDownloadBase.ToString());
        Assert.Equal("github.com", checker.ReleasesPage.Host);
    }

    private static HttpClient Stub(HttpStatusCode status, string body) =>
        new(new StubHandler(status, body));

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }
}
