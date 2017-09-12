
using System;

namespace ElevatedJobOnDemand
{
    [Serializable]
    class IpcLog
    {
        private string Log { get; set; }

        public IpcLog(string log)
        {
            Log = log;
        }
    }
}
