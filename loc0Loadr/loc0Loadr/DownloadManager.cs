using System;
using System.Threading.Tasks;

namespace loc0Loadr
{
    internal class DownloadManager
    {
        public async Task Run()
        {
            if (string.IsNullOrWhiteSpace(Configuration.GetValue<string>("arl")))
            {
                Helpers.RedMessage("ARL is missing");

                string arl = Helpers.TakeInput("Enter ARL: ");

                if (!Configuration.UpdateConfig("arl", arl))
                {
                    Helpers.RedMessage("Failed to update config");
                    Environment.Exit(1);
                }
            }

            var deezerActions = new DeezerActions(Configuration.GetValue<string>("arl"));

            if (!await deezerActions.GetApiToken())
            {
                Helpers.RedMessage("Failed to get API token");
                Environment.Exit(1);
            }

            while (true)
            {
                string url = Helpers.TakeInput("Enter URL: ");
            }
        }
    }
}