
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace ElevatedJobOnDemand
{
    [Serializable]
    public class IpcMessage
    {
        public IpcCommand IpcCommand { get; set; }
        private object IpcData { get; set; }

        public IpcMessage(IpcCommand command, object data)
        {
            IpcCommand = command;
            IpcData = data;
        }

        public T IpcDataAs<T>() where T : class
        {
            if (IpcData is T)
                return (T)IpcData;
            return null;
        }

        public static IpcMessage FromMessage(MemoryStream stream)
        {
            IpcMessage msg = null;
            if (stream.Length > 0)
            {
                BinaryFormatter formatter = new BinaryFormatter();
                msg = formatter.Deserialize(stream) as IpcMessage;
            }
            return msg;
        }

        public MemoryStream ToMessage()
        {
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream stream = new MemoryStream();
            formatter.Serialize(stream, this);
            return stream;
        }
    }

    public enum IpcCommand
    {
        IpcRequestExit,
        IpcResponseExit,
        IpcRequestUpdatePplpmRules,
        IpcResponseUpdatePplpmRules,
        IpcLog
    }
}
