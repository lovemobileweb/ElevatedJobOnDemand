
using System.Threading;

namespace ElevatedJobOnDemand
{
    class ElevatedJobProvider
    {
        private static string PipeName { get; set; }
        private static IpcModule IpcServer { get; set; }

        public void Provide(string pipeName)
        {
            PipeName = pipeName;

            IpcServer = new IpcModule();
            IpcServer.StartIpcServer(PipeName);

            while (IpcServer.IsRunning)
            {
                Thread.Sleep(100);
            }
        }
    }
}
