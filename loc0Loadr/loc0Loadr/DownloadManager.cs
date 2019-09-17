using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using loc0Loadr.Enums;

namespace loc0Loadr
{
    internal class DownloadManager
    {
        private DeezerHttp _deezerHttp;

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

            _deezerHttp = new DeezerHttp(Configuration.GetValue<string>("arl"));

            if (!await _deezerHttp.GetApiToken())
            {
                Helpers.RedMessage("Failed to get API token");
                Environment.Exit(1);
            }
            
            Helpers.GreenMessage("Success");

            while (true)
            {
                string choice = Helpers.TakeInput(1, 2, "1 - Download via URL", "2 - Search for media");
                string qualityChoice = Helpers.TakeInput(1, 4, "1 - MP3 128", "2 - MP3 256", "3 - MP3 320", "4 - FLAC");

                AudioQuality quality = Helpers.InputToAudioQuality[qualityChoice];

                switch (choice)
                {
                    case "1":
                        await DownloadFromUrl(quality);
                        break;
                    case "2":
                        await DownloadFromSearch(quality);
                        break;
                }
            } 
        }

        private async Task DownloadFromUrl(AudioQuality quality)
        {
            string url = Helpers.TakeInput("Enter URL: ");

            string[] urlMatches = Regex.Split(url, @"\/(\w+)\/(\d+)"); // ty smloadr for the regex

            if (urlMatches.Length < 3)
            {
                Helpers.RedMessage("Invalid URL");
                return;
            }

            string type = urlMatches[1];
            string id = urlMatches[2];
            
            switch (type)
            {
                case "track":
                    bool result = await _deezerHttp.StartSingleDownload(id, quality);
                    break;
                case "album":
                    
                    break;
            }
        }

        private async Task DownloadFromSearch(AudioQuality quality)
        {
            
        }
    }
}