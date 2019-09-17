using System;
using System.Text.RegularExpressions;
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
                var x = await deezerActions.Search("");
                string url = Helpers.TakeInput("Enter URL: ");

                string[] urlMatches = Regex.Split(url, @"\/(\w+)\/(\d+)"); // ty smloadr for the regex

                if (urlMatches.Length < 3)
                {
                    Helpers.RedMessage("Invalid URL");
                    continue;
                }

                string type = urlMatches[1];
                string id = urlMatches[2];

                switch (type)
                {
                    case "track":
                        bool result = await deezerActions.StartSingleDownload(id);
                        break;
                }
            } 
        }
    }
}