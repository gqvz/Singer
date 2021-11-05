using System;
using System.Collections.Generic;
using DSharpPlus.CommandsNext;
using DSharpPlus.SlashCommands;
using Singer.Constructs;

namespace Singer
{
    public static class Helpers
    {
        public static void StartLavalink()
        {
            var process = new System.Diagnostics.Process(); // use `var` instead of implicit types, looks clearer, works the same, doesnt take up half the screen
            var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    RedirectStandardOutput = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    FileName = "cmd.exe",
                    Arguments = "/C \"C:\\Program Files (x86)\\Minecraft Launcher\\runtime\\java-runtime-alpha\\windows-x64\\java-runtime-alpha\\bin\\java.exe\" -jar Lavalink.jar" // thats the weird place for java to be
                };
            process.StartInfo = startInfo;
            process.Start();
        }
        
        public static bool IsBoundChannel(this CommandContext ctx, Player player)
        {
            return ctx.Channel == player.TextChannel;
        }
        
        public static bool IsBoundChannel(this InteractionContext ctx, Player player)
        {
            return ctx.Channel == player.TextChannel;
        }
        
        // Shuffle the songs in the array
        public static void Shuffle(this List<Song> array)
        {
            var rng = new Random();
            var n = array.Count;
            while (n > 1)
            {
                var k = rng.Next(n--);
                (array[n], array[k]) = (array[k], array[n]); // listen to rider, it is cool
            }
        }
    }
}