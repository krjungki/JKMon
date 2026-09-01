using System.Net;
using System.Text;
using JKMon.Core.Settings;
using JKMon.Core.Update;

namespace JKMon.Core.Tests;

public class UpdateCheckTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Schedule_NeverBlocksEveryAutomaticCheck()
    {
        Assert.False(UpdateSchedule.IsDue(UpdateCheckFrequency.Never, default, Now, atStartup: true, checkAtStartup: true));
        Assert.False(UpdateSchedule.IsDue(UpdateCheckFrequency.Never, default, Now, atStartup: false, checkAtStartup: true));
    }

    [Fact]
    public void Schedule_StartupCheckObeysItsOwnSwitch()
    {
        Assert.False(UpdateSchedule.IsDue(UpdateCheckFrequency.Daily, default, Now, atStartup: true, checkAtStartup: false));
        Assert.True(UpdateSchedule.IsDue(UpdateCheckFrequency.Daily, default, Now, atStartup: true, checkAtStartup: true));
    }

    [Fact]
    public void Schedule_FirstCheckIsAlwaysDue()
    {
        Assert.True(UpdateSchedule.IsDue(UpdateCheckFrequency.Weekly, default, Now, atStartup: false, checkAtStartup: false));
    }

    [Theory]
    [InlineData(UpdateCheckFrequency.Daily, 23, false)]
    [InlineData(UpdateCheckFrequency.Daily, 24, true)]
    [InlineData(UpdateCheckFrequency.Weekly, 167, false)]
    [InlineData(UpdateCheckFrequency.Weekly, 168, true)]
    public void Schedule_WaitsForTheInterval(UpdateCheckFrequency frequency, int hoursSince, bool expected)
    {
        var last = Now.AddHours(-hoursSince);

        Assert.Equal(expected, UpdateSchedule.IsDue(frequency, last, Now, atStartup: false, checkAtStartup: false));
    }

    [Fact]
    public void Schedule_RecoversFromAClockThatMovedBackwards()
    {
        var last = Now.AddDays(5);

        Assert.True(UpdateSchedule.IsDue(UpdateCheckFrequency.Daily, last, Now, atStartup: false, checkAtStartup: false));
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
