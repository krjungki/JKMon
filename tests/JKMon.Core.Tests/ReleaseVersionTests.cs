using JKMon.Core.Update;

namespace JKMon.Core.Tests;

public class ReleaseVersionTests
{
    [Theory]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("V0.1.0", 0, 1, 0)]
    [InlineData("  10.20.30  ", 10, 20, 30)]
    public void TryParse_AcceptsThreePartVersions(string text, int major, int minor, int patch)
    {
        Assert.True(ReleaseVersion.TryParse(text, out var version));
        Assert.Equal(new ReleaseVersion(major, minor, patch, string.Empty), version);
    }

    [Fact]
    public void TryParse_KeepsAPrereleaseSuffix()
    {
        Assert.True(ReleaseVersion.TryParse("1.2.3-beta.1", out var version));

        Assert.Equal("beta.1", version.Suffix);
        Assert.Equal("1.2.3-beta.1", version.ToString());
    }

    [Fact]
    public void TryParse_DiscardsBuildMetadata()
    {
        Assert.True(ReleaseVersion.TryParse("0.2.1+ad379f8e56557b3567c8dbafb89b3439e512b203", out var version));

        Assert.Equal(string.Empty, version.Suffix);
        Assert.Equal("0.2.1", version.ToString());
    }

    [Fact]
    public void ABuildStampedWithItsCommitIsNotOlderThanTheRelease()
    {
        // The SDK appends the commit hash, which used to make an installed build ask to update to itself.
        Assert.True(ReleaseVersion.TryParse("0.2.1+ad379f8e", out var installed));
        Assert.True(ReleaseVersion.TryParse("0.2.1", out var published));

        Assert.False(installed < published);
        Assert.Equal(installed, published);
    }

    [Fact]
    public void PrereleaseMetadataStillOrdersBeforeTheRelease()
    {
        Assert.True(ReleaseVersion.TryParse("1.0.0-rc.1+abc123", out var candidate));
        Assert.True(ReleaseVersion.TryParse("1.0.0", out var release));

        Assert.Equal("rc.1", candidate.Suffix);
        Assert.True(candidate < release);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("one.two.three")]
    [InlineData("-1.0.0")]
    public void TryParse_RejectsAnythingElse(string? text)
    {
        Assert.False(ReleaseVersion.TryParse(text, out _));
    }

    [Fact]
    public void Compare_OrdersByMajorThenMinorThenPatch()
    {
        Assert.True(Parse("1.0.0") > Parse("0.9.9"));
        Assert.True(Parse("0.2.0") > Parse("0.1.9"));
        Assert.True(Parse("0.1.2") > Parse("0.1.1"));
        Assert.True(Parse("0.1.0") <= Parse("0.1.0"));
    }

    [Fact]
    public void Compare_TreatsAPrereleaseAsOlderThanItsRelease()
    {
        Assert.True(Parse("1.0.0") > Parse("1.0.0-rc.1"));
        Assert.True(Parse("1.0.0-rc.2") > Parse("1.0.0-rc.1"));
    }

    [Fact]
    public void AnUnreadableCurrentVersionNeverLooksNewerThanARelease()
    {
        Assert.True(Parse("0.1.0") > ReleaseVersion.Zero);
    }

    private static ReleaseVersion Parse(string text)
    {
        Assert.True(ReleaseVersion.TryParse(text, out var version));
        return version;
    }
}
