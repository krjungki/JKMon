using JKMon.Core.Settings;

namespace JKMon.Core.Tests;

public class AppPathsTests
{
    [Fact]
    public void DataRoot_IsAnAbsolutePathWithoutATrailingSeparator()
    {
        var root = AppPaths.DataRoot;

        Assert.True(Path.IsPathFullyQualified(root));
        Assert.Equal(root, Path.TrimEndingDirectorySeparator(root));
    }

    [Fact]
    public void StateFiles_SitDirectlyInTheDataRoot()
    {
        Assert.Equal(AppPaths.DataRoot, Path.GetDirectoryName(AppPaths.SettingsFile));
        Assert.Equal(AppPaths.DataRoot, Path.GetDirectoryName(AppPaths.DiagnosticsFile));
    }

    [Fact]
    public void DataRoot_IsStableAcrossCalls()
    {
        Assert.Equal(AppPaths.DataRoot, AppPaths.DataRoot);
    }

    /// <summary>The test host runs from a writable output folder, so the portable branch is the one exercised.</summary>
    [Fact]
    public void IsPortable_TracksTheExecutableFolder()
    {
        Assert.True(AppPaths.IsPortable);
        Assert.Equal(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), AppPaths.DataRoot);
    }
}
