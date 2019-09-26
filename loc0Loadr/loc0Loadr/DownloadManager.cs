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
            Console.CancelKeyPress += ConsoleOnCancelKeyPress;
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
                string choice = Helpers.TakeInput(1, 3, "Download via URL", "Search for media", "Exit");

                if (choice == "3")
                {
                    _deezerHttp.Dispose();
                    Environment.Exit(0);
                }
                
                string qualityChoice = Helpers.TakeInput(1, 4, "MP3 128", "MP3 256", "MP3 320", "FLAC");

                AudioQuality quality = Helpers.InputToAudioQuality[qualityChoice];

                // ReSharper disable once ConvertIfStatementToSwitchStatement
                if (choice == "1")
                {
                    await DownloadFromUrl(quality);
                }
                else if (choice == "2")
                {
                    await DownloadFromSearch(quality);
                }
            }
        }

        private async Task DownloadFromUrl(AudioQuality audioQuality)
        {
            string url = Helpers.TakeInput("Enter URL: ");

            string[] urlMatches = Regex.Split(url, @"https?:\/\/www\.deezer\..+\/(\w+)\/(\d+)"); // ty smloadr for the regex

            if (urlMatches.Length < 3)
            {
                Helpers.RedMessage("Invalid URL");
                return;
            }

            string type = urlMatches[1];
            string id = urlMatches[2];
            
            var deezerDownloader = new DeezerDownloader(_deezerHttp, audioQuality);

            switch (type)
            {
                case "track":
                    await deezerDownloader.ProcessTrack(id);
                    Console.Write("\n");
                    break;
                case "artist":
                    await deezerDownloader.ProcessArtist(id);
                    break;
                case "playlist":
                    await deezerDownloader.DownloadPlaylist(id);
                    break;
                case "album":
                    await deezerDownloader.ProcessAlbum(id);
                    break;
                default:
                    Helpers.RedMessage($"{type} is an unsupported type");
                    break;
            }
        }

        private async Task DownloadFromSearch(AudioQuality quality)
        {
            
        }

        private void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            _deezerHttp?.Dispose();
        }
    }
}