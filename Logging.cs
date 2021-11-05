using System;

namespace Singer
{
    public class Logging
    {
        public static void Log_Info(string s)
        {
            Console.Write("[");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write("INFO");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"]: {s}");
        }
        
        public static void Log_Warn(string s)
        {
            Console.Write("[");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("WARN");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"]: {s}");
        }
        
        public static void Log_Critical(string s)
        {
            Console.Write("[");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("CRITICAL");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"]: {s}");
        }
    }
}