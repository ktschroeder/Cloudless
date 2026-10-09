using System.Reflection;
using System.Reflection.Emit;
using Cloudless.PluginBase;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace Cloudless.Tests;

public class PluginManagerTests
{
    [Fact]
    public void CreateCommands_InstantiatesEveryConcretePluginImplementationInTheAssembly()
    {
        var plugins = PluginManager.CreateCommands(typeof(FirstTestPlugin).Assembly).ToList();

        Assert.Collection(
            plugins.OrderBy(plugin => plugin.Name),
            plugin => Assert.Equal("First test plugin", plugin.Name),
            plugin => Assert.Equal("Second test plugin", plugin.Name));
    }

    [Fact]
    public void CreateCommands_ThrowsWhenAssemblyHasNoPluginImplementation()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"Cloudless.EmptyPluginTests.{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);

        var exception = Assert.Throws<ApplicationException>(() => PluginManager.CreateCommands(assembly).ToList());

        Assert.Contains("Can't find any type which implements IPlugin", exception.Message);
    }
}

public abstract class AbstractTestPlugin : IPlugin
{
    public abstract string Name { get; }
    public abstract string PluginVersion { get; }
    public abstract string MinAppVersion { get; }
    public abstract string Description { get; }
    public abstract List<string> SupportsFileTypes { get; }
    public abstract ImageSource Convert(byte[] bytes);
    public abstract Task<UIElement?> CreateView();
    public abstract Task WarmupAsync();
    public abstract void SetAnimatedSource(Image imageDisplay, BitmapImage bitmap);
    public abstract object? GetAnimationController(Image imageDisplay);
}

public class FirstTestPlugin : IPlugin
{
    public string Name => "First test plugin";
    public string PluginVersion => "1.0.0";
    public string MinAppVersion => "1.0.0";
    public string Description => "Test fixture";
    public List<string> SupportsFileTypes => new() { "gif" };
    public ImageSource Convert(byte[] bytes) => null!;
    public Task<UIElement?> CreateView() => Task.FromResult<UIElement?>(null);
    public Task WarmupAsync() => Task.CompletedTask;
    public void SetAnimatedSource(Image imageDisplay, BitmapImage bitmap) { }
    public object? GetAnimationController(Image imageDisplay) => null;
}

public sealed class GenericTestPlugin<T> : FirstTestPlugin
{
}

public sealed class SecondTestPlugin : IPlugin
{
    public string Name => "Second test plugin";
    public string PluginVersion => "2.0.0";
    public string MinAppVersion => "1.0.0";
    public string Description => "Test fixture";
    public List<string> SupportsFileTypes => new() { "webp" };
    public ImageSource Convert(byte[] bytes) => null!;
    public Task<UIElement?> CreateView() => Task.FromResult<UIElement?>(null);
    public Task WarmupAsync() => Task.CompletedTask;
    public void SetAnimatedSource(Image imageDisplay, BitmapImage bitmap) { }
    public object? GetAnimationController(Image imageDisplay) => null;
}
