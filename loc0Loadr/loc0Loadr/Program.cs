using System;
using System.Threading.Tasks;

namespace loc0Loadr
{
    internal static class Program
    {
        private static async Task Main()
        {
            if (!Configuration.GetConfig())
            {
                Environment.Exit(1);
            }

            var manager = new DownloadManager();

            await manager.Run();
        }
    }
}