using System;
using System.Linq;
using System.Threading.Tasks;
using loc0Loadr.Enums;

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