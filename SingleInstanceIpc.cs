using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cloudless
{
    public static class SingleInstanceIpc
    {
        private const string PipeName = "CloudlessPipe";

        private static CancellationTokenSource? _cts;
        private static Task? _serverTask;

        // Fired when another instance sends us a message
        public static event Action<string>? MessageReceived;

        // Server (primary instance)
        public static void StartServer()
        {
            _cts = new CancellationTokenSource();

            _serverTask = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream(
                            PipeName,
                            PipeDirection.In,
                            1,
                            PipeTransmissionMode.Message,
                            PipeOptions.Asynchronous);

                        await server.WaitForConnectionAsync(_cts.Token);

                        using var reader = new StreamReader(server, Encoding.UTF8);
                        string message = await reader.ReadToEndAsync();

                        // Invoke handlers on threadpool to avoid blocking the pipe loop
                        var handlers = MessageReceived; // capture
                        if (handlers != null)
                        {
                            foreach (Action<string> h in handlers.GetInvocationList())
                            {
                                Task.Run(() =>
                                {
                                    try { h(message); }
                                    catch { }
                                });
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // normal shutdown
                        break;
                    }
                    catch
                    {
                        // swallow and continue listening
                    }
                }
            });
        }

        public static void StopServer()
        {
            // Synchronous wrapper for callers that expect a blocking stop
            StopServerAsync().GetAwaiter().GetResult();
        }

        public static async Task StopServerAsync()
        {
            try
            {
                _cts?.Cancel();

                if (_serverTask != null)
                {
                    // Wait briefly for the server task to finish
                    await Task.WhenAny(_serverTask, Task.Delay(2000)).ConfigureAwait(false);
                }
            }
            catch
            {
                // ignore
            }
            finally
            {
                try { _cts?.Dispose(); } catch { }
                _cts = null;
                _serverTask = null;
            }
        }

        // Client (secondary instance)
        public static bool SendMessageToPrimary(string message)
        {
            try
            {
                using var client = new NamedPipeClientStream(
                    ".",
                    PipeName,
                    PipeDirection.Out);

                client.Connect(200); // ms timeout

                using var writer = new StreamWriter(client, Encoding.UTF8)
                {
                    AutoFlush = true
                };

                writer.Write(message);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
