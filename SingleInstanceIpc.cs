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

        // Fired when another instance sends us a message
        public static event Action<string>? MessageReceived;

        // Server (primary instance)
        public static void StartServer()
        {
            _cts = new CancellationTokenSource();

            Task.Run(async () =>
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

                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            MessageReceived?.Invoke(message);
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
            _cts?.Cancel();
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
