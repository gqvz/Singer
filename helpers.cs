using System;
using System.Collections.Generic;
using DSharpPlus.CommandsNext;
using DSharpPlus.SlashCommands;

namespace Singer
{
    public static class Helpers
    {
        public static void StartLavalink()
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    RedirectStandardOutput = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    FileName = "cmd.exe",
                    Arguments = "/C \"C:\\Program Files (x86)\\Minecraft Launcher\\runtime\\java-runtime-alpha\\windows-x64\\java-runtime-alpha\\bin\\java.exe\" -jar Lavalink.jar"
                };
            process.StartInfo = startInfo;
            process.Start();
        }
        
        public static bool IsBoundChannel(CommandContext ctx, Player player)
        {
            return ctx.Channel == player.TextChannel;
        }
        
        public static bool IsBoundChannel(InteractionContext ctx, Player player)
        {
            return ctx.Channel == player.TextChannel;
        }
        
        // Shuffle the songs in the array
        public static void Shuffle(this List<Song> array)
        {
            var rng = new Random();
            int n = array.Count;
            while (n > 1)
            {
                int k = rng.Next(n--);
                Song temp = array[n];
                array[n] = array[k];
                array[k] = temp;
            }
        }
    }
}