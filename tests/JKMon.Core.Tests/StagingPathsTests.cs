using JKMon.Core.Update;

namespace JKMon.Core.Tests;

public class StagingPathsTests
{
    private const string Temp = @"C:\Temp";

    [Fact]
    public void WorkRoot_IsNamedForTheVersion()
    {
        ReleaseVersion.TryParse("0.2.0", out var version);

        var root = StagingPaths.WorkRootFor(Temp, version);

        Assert.Equal(@"C:\Temp\JKMon-update-0.2.0", root);
        Assert.True(StagingPaths.IsStagingRoot(root, Temp));
    }

    [Theory]
    [InlineData(@"C:\Temp\JKMon-update-0.2.0")]
    [InlineData(@"C:\Temp\JKMon-update-0.2.0\")]
    [InlineData(@"C:\Temp\JKMon-update-anything")]
    public void IsStagingRoot_AcceptsOurOwnFolders(string path)
    {
        Assert.True(StagingPaths.IsStagingRoot(path, Temp));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"C:\Temp")]
    [InlineData(@"C:\Temp\something-else")]
    [InlineData(@"C:\Program Files\JKMon-update-0.2.0")]
    [InlineData(@"C:\Temp\nested\JKMon-update-0.2.0")]
    [InlineData(@"C:\Temp\JKMon-update-0.2.0\staged")]
    [InlineData(@"C:\Temp\..\Windows")]
    [InlineData(@"C:\Temp\JKMon-update-0.2.0\..\..\Windows")]
    public void IsStagingRoot_RefusesEverythingElse(string? path)
    {
        Assert.False(StagingPaths.IsStagingRoot(path, Temp));
    }

    [Fact]
    public void IsStagingRoot_IsCaseInsensitiveOnTheRootButNotOnThePrefix()
    {
        Assert.True(StagingPaths.IsStagingRoot(@"c:\temp\JKMon-update-1.0.0", Temp));
        Assert.False(StagingPaths.IsStagingRoot(@"C:\Temp\jkmon-update-1.0.0", Temp));
    }
}
