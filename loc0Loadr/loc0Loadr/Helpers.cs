using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace loc0Loadr
{
    internal static class Helpers
    {
        public static void RedMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"\n{message}");
            Console.ForegroundColor = ConsoleColor.White;
        }
        
        public static void GreenMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n{message}");
            Console.ForegroundColor = ConsoleColor.White;
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

        public static string TakeInput(int start, int count, params string[] messages)
        {
            Console.WriteLine($"\n{start} - {messages[0]}");
                
            for (var i = 1; i < messages.Length; i++)
            {
                string message = messages[i];

                Console.WriteLine($"{i + 1} - {message}");
            }

            Console.Write("\nEnter choice: ");
            string input = Console.ReadLine()?.Trim();

            while (!int.TryParse(input, out int number)
            || !Enumerable.Range(start, count).Contains(number))
            {
                RedMessage("Invalid Input");
                Console.Write("\nEnter choice: ");
                input = Console.ReadLine()?.Trim();
            }

            return input;
        }

        public static string SanitseString(this string word)
        {
            word = word.Trim();
            
            var invalidChars = Path.GetInvalidFileNameChars();
            word = string.Join("_", word.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

            word = Regex.Replace(word, @"\s(\s+)", "$1");

            return word;
        }

        public static string PadNumber(this string word)
        {
            return int.Parse(word) < 10 
                ? $"0{word}" 
                : word;
        }
    }
}