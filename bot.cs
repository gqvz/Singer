using System;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.EventHandling;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.Lavalink;
using DSharpPlus.Net;
using DSharpPlus.SlashCommands;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Singer.Commands;
using WenceyWang.FIGlet;


namespace Singer
{
    class Singer
    {
        static async Task Bot()
        {
            Logging.Log_Info("Initializing Bot...");
            Logging.Log_Info("Starting Lavalink...");
            Helpers.StartLavalink();
            await Task.Delay(5);
            var discord = new DiscordClient(new DiscordConfiguration()
            {
                Token = Config.Token,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.All,
                MinimumLogLevel = LogLevel.Warning
            });

            var commands = discord.UseCommandsNext(new CommandsNextConfiguration()
            {
                StringPrefixes = Config.Prefixes,
                EnableMentionPrefix = true,
                EnableDefaultHelp = true 
            });

            Logging.Log_Info("Initialized command handler.");

            commands.RegisterCommands(Assembly.GetExecutingAssembly());
            Logging.Log_Info("Registered commands.");

            Logging.Log_Info("Initializing and registering slash commands.");
            var slash = discord.UseSlashCommands();
            slash.RegisterCommands<SlashTest>();

            Logging.Log_Info("Configuring Lavalink endpoint.");
            var endpoint = new ConnectionEndpoint
            {
                Hostname = "127.0.0.1",
                Port = 62579
            };

            var lavalinkConfig = new LavalinkConfiguration
            {
                Password = "singertotherescue",
                RestEndpoint = endpoint,
                SocketEndpoint = endpoint
            };
            
            Logging.Log_Info("Configuring Interactivity session.");
            discord.UseInteractivity(new InteractivityConfiguration() 
            { 
                AckPaginationButtons = true,
                PaginationButtons = new PaginationButtons(),
                ButtonBehavior = ButtonPaginationBehavior.DeleteButtons,
                Timeout = TimeSpan.FromSeconds(300),
                PaginationBehaviour = PaginationBehaviour.WrapAround
            });


            var lavalink = discord.UseLavalink();
            Logging.Log_Info("Lavalink initialized and registered.");

            discord.Ready += on_ready;
            discord.MessageCreated += on_message;
            discord.GuildMemberAdded += on_guild_member_add;
            discord.GuildMemberRemoved += on_guild_member_remove;
            Logging.Log_Info("Registered event handlers.");

            Logging.Log_Info("\n" + new AsciiArt("Singer") + "\n");

            await discord.ConnectAsync();
            await lavalink.ConnectAsync(lavalinkConfig);
            await Task.Delay(-1);
        }

#pragma warning disable 1998
        static async Task on_ready(DiscordClient self, ReadyEventArgs e)
        {
            Logging.Log_Info($"Connected to Discord as {self.CurrentUser}");
        }

        static async Task on_message(DiscordClient self, MessageCreateEventArgs e)
        {
            if (e.Author == self.CurrentUser)
                return;

            Logging.Log_Info($"Message received from {e.Author} in {e.Guild}: {e.Message.Content}");
        }
        
       // Log to console when a user joins the server
        static async Task on_guild_member_add(DiscordClient self, GuildMemberAddEventArgs e)
        {
            Logging.Log_Info($"{e.Member} has joined {e.Guild}");
        }
        
        // Log to console when a user leaves the server
        static async Task on_guild_member_remove(DiscordClient self, GuildMemberRemoveEventArgs e)
        {
            Logging.Log_Info($"{e.Member} has left {e.Guild}");
        }

#pragma warning restore 1998

        static void Main(string[] args)
        {
            Bot().GetAwaiter().GetResult();
        }
    }
}