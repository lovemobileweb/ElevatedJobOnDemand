using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;

namespace ElevatedJobOnDemand
{
    class ElevatedJobRequester
    {
        private static string PipeName { get; set; }

        public void Request()
        {
            Process elevatedProcess = new Process();

            elevatedProcess.StartInfo.FileName = Process.GetCurrentProcess().MainModule.FileName;

            // Pass a pipe name to the client process
            PipeName = string.Format("ElevatedJobOnDemand-{0}", Process.GetCurrentProcess().Id);
            try
            {
                elevatedProcess.StartInfo.Arguments = PipeName;
                elevatedProcess.StartInfo.UseShellExecute = true;
                elevatedProcess.StartInfo.Verb = "runas";
                elevatedProcess.Start();

                IpcMessage res = IpcModule.GetIpcResponse(PipeName, new IpcMessage(IpcCommand.IpcRequestUpdatePplpmRules, new IpcRequestUpdateRules() { GpoId = "111" }));
                if (res != null)
                {
                    Console.WriteLine(res.IpcDataAs<IpcLog>().ToString());
                }
                IpcModule.GetIpcResponse(PipeName, new IpcMessage(IpcCommand.IpcRequestExit, null));
                elevatedProcess.WaitForExit();
                elevatedProcess.Close();
            }
            catch (Exception)
            {
            }
        }
    }
}
