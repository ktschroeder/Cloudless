using Cloudless.PluginBase;
using Cloudless.PluginBase;
using System.Windows.Threading;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
public sealed class VlcPluginDiscoveryTests : IClassFixture<VlcPluginFixture>
{
    private readonly VlcPluginFixture _fixture;

    public VlcPluginDiscoveryTests(VlcPluginFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "PluginIntegration")]
    public void PluginManagerDiscoversVlcPluginForItsSupportedFileTypes()
    {
        var vlcPlugin = _fixture.Plugin;

        Assert.Contains("mp4", vlcPlugin.SupportsFileTypes, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("mkv", vlcPlugin.SupportsFileTypes, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("webm", vlcPlugin.SupportsFileTypes, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(_fixture.Plugins, plugin => plugin.SupportsFileTypes.Contains("jpg", StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "PluginIntegration")]
    public async Task PluginManagerLoadedVlcPluginCreatesAnIVideoPlayerView()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var view = await _fixture.Plugin.CreateView();
            var videoPlayer = Assert.IsAssignableFrom<Cloudless.PluginBase.IVideoPlayer>(view);
            try
            {
                Assert.Equal(TimeSpan.Zero, videoPlayer.GetDuration());
                videoPlayer.SetVolume(37);
                Assert.Equal(37, videoPlayer.GetVolume());
                videoPlayer.Mute();
                Assert.True(videoPlayer.IsMuted());
                videoPlayer.Unmute();
                Assert.False(videoPlayer.IsMuted());
            }
            finally
            {
                videoPlayer.Dispose();
            }
        });
    }

    private static Task RunOnStaThreadAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

            try
            {
                var task = action();
                task.ContinueWith(
                    _ => dispatcher.BeginInvokeShutdown(DispatcherPriority.Background),
                    TaskScheduler.Default);
                Dispatcher.Run();
                task.GetAwaiter().GetResult();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}

public sealed class VlcPluginFixture
{
    public List<IPlugin> Plugins { get; }
    public IPlugin Plugin { get; }

    public VlcPluginFixture()
    {
        Plugins = PluginManager.GetPlugins().ToList();
        Plugin = Plugins.Single(plugin => plugin.Name == "VLC Plugin");
    }
}
