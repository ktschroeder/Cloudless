using System.IO;
using Xunit;

namespace Cloudless.Tests;

public sealed class PluginDiscoveryTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "Cloudless.Tests", Guid.NewGuid().ToString("N"));
    private const string AssemblyFileName = "Cloudless.TestPlugin.dll";

    [Fact]
    public void GetLatestPluginAssemblyPath_ReturnsNullForMissingRoot()
    {
        Assert.Null(PluginManager.GetLatestPluginAssemblyPath(Path.Combine(_testDirectory, "missing"), AssemblyFileName));
    }

    [Fact]
    public void GetLatestPluginAssemblyPath_ReturnsNullWhenRootHasNoVersionDirectories()
    {
        Directory.CreateDirectory(_testDirectory);

        Assert.Null(PluginManager.GetLatestPluginAssemblyPath(_testDirectory, AssemblyFileName));
    }

    [Fact]
    public void GetLatestPluginAssemblyPath_SelectsNewestNumericVersion()
    {
        CreateVersion("1.2.0", AssemblyFileName);
        var expected = CreateVersion("1.10.0", AssemblyFileName);

        Assert.Equal(expected, PluginManager.GetLatestPluginAssemblyPath(_testDirectory, AssemblyFileName));
    }

    [Fact]
    public void GetLatestPluginAssemblyPath_AcceptsVersionFoldersWithVPrefix()
    {
        var expected = CreateVersion("v2.3.4", AssemblyFileName);
        CreateVersion("1.9.9", AssemblyFileName);

        Assert.Equal(expected, PluginManager.GetLatestPluginAssemblyPath(_testDirectory, AssemblyFileName));
    }

    [Fact]
    public void GetLatestPluginAssemblyPath_IgnoresFoldersThatAreNotVersions()
    {
        CreateVersion("latest", AssemblyFileName);
        CreateVersion("vNext", AssemblyFileName);
        var expected = CreateVersion("1.2.3", AssemblyFileName);

        Assert.Equal(expected, PluginManager.GetLatestPluginAssemblyPath(_testDirectory, AssemblyFileName));
    }

    [Fact]
    public void GetLatestPluginAssemblyPath_FallsBackWhenNewestVersionIsIncomplete()
    {
        var expected = CreateVersion("1.0.0", AssemblyFileName);
        CreateVersion("2.0.0");

        Assert.Equal(expected, PluginManager.GetLatestPluginAssemblyPath(_testDirectory, AssemblyFileName));
    }

    [Fact]
    public void GetLatestPluginAssemblyPath_ReturnsNullWhenNoVersionContainsExpectedAssembly()
    {
        CreateVersion("1.0.0", "different-plugin.dll");
        CreateVersion("2.0.0");

        Assert.Null(PluginManager.GetLatestPluginAssemblyPath(_testDirectory, AssemblyFileName));
    }

    [Fact]
    public void GetLatestPluginAssemblyPath_UsesTheRequestedAssemblyName()
    {
        var versionDirectory = Path.Combine(_testDirectory, "1.0.0");
        Directory.CreateDirectory(versionDirectory);
        var expected = Path.Combine(versionDirectory, "requested.dll");
        File.WriteAllBytes(expected, Array.Empty<byte>());

        Assert.Equal(expected, PluginManager.GetLatestPluginAssemblyPath(_testDirectory, "requested.dll"));
        Assert.Null(PluginManager.GetLatestPluginAssemblyPath(_testDirectory, AssemblyFileName));
    }

    private string CreateVersion(string version, string? assemblyName = null)
    {
        var versionDirectory = Path.Combine(_testDirectory, version);
        Directory.CreateDirectory(versionDirectory);
        if (assemblyName == null)
            return versionDirectory;

        var assemblyPath = Path.Combine(versionDirectory, assemblyName);
        File.WriteAllBytes(assemblyPath, Array.Empty<byte>());
        return assemblyPath;
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }
}
