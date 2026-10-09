using Xunit;
using System.Threading.Channels;

namespace Cloudless.Tests;

public sealed class SingleInstanceIpcTests
{
    [Fact]
    public async Task ServerReceivesClientMessageAndSendFailsAfterServerStops()
    {
        var receivedMessages = Channel.CreateUnbounded<string>();
        Action<string> handler = message => receivedMessages.Writer.TryWrite(message);
        SingleInstanceIpc.MessageReceived += handler;

        try
        {
            SingleInstanceIpc.StartServer();

            const string imagePath = "C:\\Cloudless.Tests\\forwarded-image.png";
            Assert.True(await SendMessageEventuallyAsync(imagePath), "The client could not connect to the IPC server.");
            Assert.Equal(
                imagePath,
                await receivedMessages.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.True(await SendMessageEventuallyAsync(string.Empty), "The client could not send an empty message.");
            Assert.Equal(
                string.Empty,
                await receivedMessages.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            SingleInstanceIpc.MessageReceived -= handler;
            await SingleInstanceIpc.StopServerAsync();
        }

        Assert.False(SingleInstanceIpc.SendMessageToPrimary("after-server-stop"));
    }

    private static async Task<bool> SendMessageEventuallyAsync(string message)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < timeout)
        {
            if (SingleInstanceIpc.SendMessageToPrimary(message))
                return true;

            await Task.Delay(25);
        }

        return false;
    }
}
