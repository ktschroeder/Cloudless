using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Cloudless
{
    class PipeServer
    {
        //public async Task StartServer()
        //{
        //    while (true)
        //    {
        //        using (NamedPipeServerStream server = new NamedPipeServerStream("CloudlessImageViewerPipe", PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Message))
        //        {
        //            await server.WaitForConnectionAsync();
        //            using (StreamReader reader = new StreamReader(server, Encoding.UTF8))
        //            {
        //                string request = await reader.ReadLineAsync();
        //                if (request == "GET_STATE")
        //                {
        //                    string response = GetWindowStateJson(); // Serialize state to JSON
        //                    using (StreamWriter writer = new StreamWriter(server, Encoding.UTF8) { AutoFlush = true })
        //                    {
        //                        await writer.WriteLineAsync(response);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}
    }

}
