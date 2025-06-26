using System;

namespace Unispect.CLI.Helpers
{
    public static class ConsoleFormatting
    {
        public static void ShowBanner()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
    ██╗   ██╗███╗   ██╗██╗███████╗██████╗ ███████╗ ██████╗████████╗   ██╗   
    ██║   ██║████╗  ██║██║██╔════╝██╔══██╗██╔════╝██╔════╝╚══██╔══╝   ██║   
    ██║   ██║██╔██╗ ██║██║███████╗██████╔╝█████╗  ██║        ██║   ███████╗
    ██║   ██║██║╚██╗██║██║╚════██║██╔═══╝ ██╔══╝  ██║        ██║   ╚══██╔══╝
    ╚██████╔╝██║ ╚████║██║███████║██║     ███████╗╚██████╗   ██║      ██║   
     ╚═════╝ ╚═╝  ╚═══╝╚═╝╚══════╝╚═╝     ╚══════╝ ╚═════╝   ╚═╝      ╚═╝   
");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"                        Unity Memory Inspector CLI v{version}");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("                     https://github.com/Razchek/Unispect");
            Console.WriteLine();
            Console.ResetColor();
        }

        public static void WriteHeader(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n🔍 {message}");
            Console.WriteLine(new string('=', message.Length + 3));
            Console.ResetColor();
        }

        public static void WriteSubHeader(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n📋 {message}");
            Console.ResetColor();
        }

        public static void WriteSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ {message}");
            Console.ResetColor();
        }

        public static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ {message}");
            Console.ResetColor();
        }

        public static void WriteInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"ℹ️  {message}");
            Console.ResetColor();
        }

        public static void WriteProperty(string name, string value)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"  {name}: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(value);
            Console.ResetColor();
        }

        public static void WriteColorLine(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        public static string GetVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        }
    }
} 