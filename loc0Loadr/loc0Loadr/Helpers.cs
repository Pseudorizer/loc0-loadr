using System;

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
    }
}