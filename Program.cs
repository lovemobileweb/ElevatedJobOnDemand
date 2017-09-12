
namespace ElevatedJobOnDemand
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length >= 1)
            {
                // elevated process
                ElevatedJobProvider jobProvider = new ElevatedJobProvider();
                jobProvider.Provide(args[0]);
            }
            else
            {
                ElevatedJobRequester jobRequester = new ElevatedJobRequester();
                jobRequester.Request();
            }
        }
    }
}
