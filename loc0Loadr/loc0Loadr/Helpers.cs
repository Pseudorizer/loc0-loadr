using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;

namespace loc0Loadr
{
    internal static class Helpers
    {
        public static void RedMessage(string message)
        {
            ConsoleColor original = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"\n{message}");
            Console.ForegroundColor = original;
        }

        public static string TakeInput(string message)
        {
            Console.Write($"\n{message}");
            string input = Console.ReadLine()?.Trim();

            while (string.IsNullOrWhiteSpace(input))
            {
                RedMessage("Invalid Input");
                Console.Write($"\n{message}");
                input = Console.ReadLine()?.Trim();
            }

            return input;
        }

        public static string GetCid()
        {
            string cid = string.Empty;

            for (int i = 0; i < 9; i++)
            {
                cid += new Random().Next(1, 9);
            }

            return cid;
        }

        public static FormUrlEncodedContent BuildDeezerApiContent(string apiToken, string method)
        {
            return new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("api_version", "1.0"),
                new KeyValuePair<string, string>("api_token", apiToken),
                new KeyValuePair<string, string>("input", "3"),
                new KeyValuePair<string, string>("method", method),
                new KeyValuePair<string, string>("cid", GetCid())
            });
        }
    }
}