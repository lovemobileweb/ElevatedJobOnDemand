using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace ElevatedJobOnDemand
{
    public class IpcModule
    {
        const int BUFFER_SIZE = 4096;  // 4 KB
        private Thread thread;

        public bool IsRunning
        {
            get
            {
                return thread != null && thread.IsAlive;
            }
        }

        private static bool WriteStream(MemoryStream data, Stream to)
        {
            byte[] bLength = BitConverter.GetBytes(data.Length);
            if (bLength.Length != System.Runtime.InteropServices.Marshal.SizeOf(data.Length))
                return false;
            to.Write(bLength, 0, bLength.Length);
            data.WriteTo(to);
            return true;
        }

        private static MemoryStream ReadStream(PipeStream from)
        {
            MemoryStream data = new MemoryStream();
            int len = System.Runtime.InteropServices.Marshal.SizeOf(data.Length);
            byte[] bLength = new byte[len];
            for (int pos = 0; pos != len && from.IsConnected; )
                pos += from.Read(bLength, pos, len - pos);
            len = BitConverter.ToInt32(bLength, 0);
            data.SetLength(len);
            for (int pos = 0; pos != data.Length; )
                pos += from.Read(data.GetBuffer(), pos, (int)data.Length - pos);
            return data;
        }

        /// <summary>
        /// Named pipe client through BCL System.IO.Pipes
        /// </summary>
        public static IpcMessage GetIpcResponse(string strPipeName, IpcMessage req)
        {
            /////////////////////////////////////////////////////////////////////
            // Try to open a named pipe.
            // 
            IpcMessage res = null;
            // Prepare the pipe name
            string strServerName = ".";

            NamedPipeClientStream pipeClient = null;

            try
            {
                pipeClient = new NamedPipeClientStream(
                    strServerName,              // The server name
                    strPipeName,                // The unique pipe name
                    PipeDirection.InOut,        // The pipe is bi-directional   
                    PipeOptions.None,           // No additional parameters

                    //The server process cannot obtain identification information about 
                    //the client, and it cannot impersonate the client.
                    TokenImpersonationLevel.Anonymous);

                pipeClient.Connect(60000); // set TimeOut for connection
                pipeClient.ReadMode = PipeTransmissionMode.Byte;

                Console.WriteLine(@"The named pipe, \\{0}\{1}, is connected.",
                    strServerName, strPipeName);


                /////////////////////////////////////////////////////////////////
                // Send a message to the pipe server and receive its response.
                //                

                using (MemoryStream reqStream = req.ToMessage())
                {
                    // Send one message to the pipe.
                    if (WriteStream(reqStream, pipeClient))
                    {
                        pipeClient.Flush();
                        Console.WriteLine("[Client] Sends {0} bytes;", reqStream.Length);

                        // Receive one message from the pipe.
                        using (MemoryStream resStream = ReadStream(pipeClient))
                        {
                            Console.WriteLine("[Client] Receives {0} bytes;", resStream.Length);
                            res = IpcMessage.FromMessage(resStream);
                        }
                    }
                }
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine("[Client] Unable to open named pipe {0}\\{1}",
                   strServerName, strPipeName);
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Client] The client throws the error: {0}", ex.Message);
            }
            finally
            {
                /////////////////////////////////////////////////////////////////
                // Close the pipe.
                // 

                if (pipeClient != null)
                    pipeClient.Close();
            }

            return res;
        }

        /// <summary>
        /// Named pipe server through BCL System.IO.Pipes
        /// </summary>
        [STAThread]
        private static void IpcServerThread(object param)
        {
            NamedPipeServerStream pipeServer = null;
            string strPipeName = param as string;
            if (string.IsNullOrEmpty(strPipeName))
                return;
            try
            {
                /////////////////////////////////////////////////////////////////
                // Create a named pipe.
                // 

                // Prepare the security attributes
                // Granting everyone the full control of the pipe is just for 
                // demo purpose, though it creates a security hole.
                PipeSecurity pipeSa = new PipeSecurity();
                pipeSa.SetAccessRule(new PipeAccessRule("Everyone",
                    PipeAccessRights.ReadWrite, AccessControlType.Allow));

                // Create the named pipe
                pipeServer = new NamedPipeServerStream(
                    strPipeName,                    // The unique pipe name.
                    PipeDirection.InOut,            // The pipe is bi-directional
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,   // Message type pipe 
                    PipeOptions.None,               // No additional parameters
                    BUFFER_SIZE,                    // Input buffer size
                    BUFFER_SIZE,                    // Output buffer size
                    pipeSa,                         // Pipe security attributes
                    HandleInheritability.None       // Not inheritable
                    );

                Console.WriteLine("[Server] The named pipe, {0}, is created", strPipeName);


                while (true)
                {
                    /////////////////////////////////////////////////////////////////
                    // Wait for the client to connect.
                    // 

                    Console.WriteLine("[Server] Waiting for the client's connection...");
                    pipeServer.WaitForConnection();

                    /////////////////////////////////////////////////////////////////
                    // Read client requests from the pipe and write the response.
                    // 

                    // Receive one message from the pipe.
                    using (MemoryStream reqStream = ReadStream(pipeServer))
                    {
                        Console.WriteLine("[Server] Receives {0} bytes;", reqStream.Length);

                        // Prepare the response.
                        IpcMessage req = IpcMessage.FromMessage(reqStream);

                        if (req != null)
                        {
                            MemoryStream stream = null;
                            if (req.IpcCommand == IpcCommand.IpcRequestExit)
                            {
                                IpcMessage res = new IpcMessage(IpcCommand.IpcResponseExit, null);
                                stream = res.ToMessage();
                            }
                            else if (req.IpcCommand == IpcCommand.IpcRequestUpdatePplpmRules)
                            {
                                IpcMessage res = new IpcMessage(IpcCommand.IpcLog, new IpcLog("test"));
                                stream = res.ToMessage();
                            }

                            if (stream != null)
                            {
                                using (stream)
                                {
                                    // Write the response to the pipe.
                                    if (WriteStream(stream, pipeServer))
                                    {
                                        pipeServer.Flush();
                                        Console.WriteLine("[Server] Replies {0} bytes;", stream.Length);
                                    }
                                }
                            }

                            /////////////////////////////////////////////////////////////////
                            // Flush the pipe to allow the client to read the pipe's contents 
                            // before disconnecting. Then disconnect the pipe.
                            // 

                            Console.WriteLine("[Server] disconnect");
                            pipeServer.Disconnect();

                            if (req.IpcCommand == IpcCommand.IpcRequestExit)
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Server] The server throws the error: {0}", ex.Message);
            }
            finally
            {
                if (pipeServer != null)
                {
                    // Close the stream.
                    pipeServer.Close();
                }
            }
        }

        /// <summary>
        /// Named pipe server through BCL System.IO.Pipes
        /// </summary>
        public void StartIpcServer(string strPipeName)
        {
            thread = new Thread(IpcServerThread);
            thread.Start(strPipeName);
        }

        public void CloseIpcServer()
        {
            thread.Abort();
        }
    }
}
