using JKMon.Core.Settings;
using JKMon.Core.Sync;

namespace JKMon.Core.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "jkmon-tests-" + Guid.NewGuid().ToString("N"));

    private string PathFor(string name) => Path.Combine(_directory, name);

    [Fact]
    public void Load_ReturnsDefaults_WhenFileIsMissing()
    {
        var store = new SettingsStore(PathFor("missing.json"));

        Assert.Equal(2, store.Load().RefreshSeconds);
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileIsCorrupt()
    {
        var path = PathFor("corrupt.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{ not json");

        var store = new SettingsStore(path);

        Assert.Equal(2, store.Load().RefreshSeconds);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAndNormalizes()
    {
        var path = PathFor("settings.json");
        var store = new SettingsStore(path);

        store.Save(new JkMonSettings { RefreshSeconds = 99, Layer = WindowLayer.AlwaysOnTop });
        var loaded = store.Load();

        Assert.Equal(JkMonSettings.MaxRefreshSeconds, loaded.RefreshSeconds);
        Assert.Equal(WindowLayer.AlwaysOnTop, loaded.Layer);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsTheAutostartFlag()
    {
        var store = new SettingsStore(PathFor("autostart.json"));

        store.Save(new JkMonSettings { StartWithWindows = true });

        Assert.True(store.Load().StartWithWindows);
    }

    [Fact]
    public void DefaultPath_SitsInThePortableDataRoot()
    {
        Assert.Equal(AppPaths.SettingsFile, new SettingsStore().FilePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}

public class SyncthingConfigReaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "jkmon-cfg-" + Guid.NewGuid().ToString("N"));

    private string WriteConfig(string xml)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "config.xml");
        File.WriteAllText(path, xml);
        return path;
    }

    [Fact]
    public void TryRead_ReturnsNull_WhenFileIsMissing()
    {
        Assert.Null(SyncthingConfigReader.TryRead(Path.Combine(_directory, "nope.xml")));
    }

    [Fact]
    public void TryRead_ParsesLoopbackEndpoint()
    {
        var path = WriteConfig(
            "<configuration><gui enabled=\"true\" tls=\"false\">" +
            "<address>127.0.0.1:8384</address><apikey>abc123</apikey></gui></configuration>");

        var endpoint = SyncthingConfigReader.TryRead(path);

        Assert.NotNull(endpoint);
        Assert.Equal("http://127.0.0.1:8384/", endpoint!.BaseAddress.ToString());
        Assert.Equal("abc123", endpoint.ApiKey);
    }

    [Fact]
    public void TryRead_UsesHttps_WhenTlsIsEnabled()
    {
        var path = WriteConfig(
            "<configuration><gui tls=\"true\">" +
            "<address>127.0.0.1:8384</address><apikey>abc123</apikey></gui></configuration>");

        Assert.Equal("https", SyncthingConfigReader.TryRead(path)!.BaseAddress.Scheme);
    }

    [Fact]
    public void TryRead_RejectsNonLoopbackAddress()
    {
        var path = WriteConfig(
            "<configuration><gui tls=\"false\">" +
            "<address>192.168.1.50:8384</address><apikey>abc123</apikey></gui></configuration>");

        Assert.Null(SyncthingConfigReader.TryRead(path));
    }

    [Fact]
    public void TryRead_ReturnsNull_WhenApiKeyIsAbsent()
    {
        var path = WriteConfig(
            "<configuration><gui tls=\"false\"><address>127.0.0.1:8384</address></gui></configuration>");

        Assert.Null(SyncthingConfigReader.TryRead(path));
    }

    [Fact]
    public void TryRead_ReturnsNull_WhenXmlIsMalformed()
    {
        Assert.Null(SyncthingConfigReader.TryRead(WriteConfig("<configuration>")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
