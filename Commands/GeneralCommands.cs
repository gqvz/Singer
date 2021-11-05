using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.EventHandling;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.Lavalink;
using DSharpPlus.Lavalink.EventArgs;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Singer.Constructs;

namespace Singer.Commands
{
    public class GeneralCommands : BaseCommandModule
    {
        public static readonly Dictionary<ulong, Player> Players = new();

        private readonly Random _rand = new();

        public static readonly string[] Gifs =
        {
            "https://i.pinimg.com/originals/d9/d4/40/d9d4406eda8b13a30a6a0de486f93402.gif",
            "https://c.tenor.com/QM-si3_EAyIAAAAC/listening-to-music-dancing.gif",
            "https://c.tenor.com/PaWj2HJ3rBsAAAAM/listening-to-music-headphones.gif",
            "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcSw2JnhuDTVvQcTmCRV6ilun4-uYXRCbLc_-lH1nT-O0PErsMIBgj3uYiFnjt_AfZJEQV4&usqp=CAU",
            "https://c.tenor.com/2wq1PfInyYkAAAAj/music-note-dancing.gif",
            "https://media4.giphy.com/media/lqSDx8SI1916ysr4eq/giphy.gif",
            "https://i.imgur.com/XpSTZyO.gif",
            "https://66.media.tumblr.com/a3e09f0d1bd335398e5211409a5203e9/tumblr_pb3qmdVXgy1vaxnh8o1_400.gif",
            "https://media4.giphy.com/media/eyyPyQluieri8/200w.gif",
            "http://pa1.narvii.com/6191/c83cf4593af7031d1c8a132f77bff5fe6790ccc6_00.gif"
        };

        [Command("play"), Description("Plays a song from a given search query or a URL."), Aliases("p")]
        public async Task Play(CommandContext ctx,
            [RemainingText, Description("A song's name or its URL.")]
            string search)
        {
            try
            {
                dynamic query;
                try
                {
                    query = new Uri(search, UriKind.Absolute);
                }
                catch
                {
                    query = new string(search);
                }

                if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
                {
                    await ctx.RespondAsync("You are not in a voice channel!");
                    return;
                }

                var lava = ctx.Client.GetLavalink();
                var node = lava.ConnectedNodes.Values.First();
                var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

                if (conn == null)
                {
                    await join_channel(ctx, ctx.Member.VoiceState.Channel);
                    conn = node.GetGuildConnection(ctx.Guild);
                    await conn.SetVolumeAsync(50);
                }
                else
                {
                    if (conn.Channel != ctx.Member.VoiceState.Channel)
                    {
                        await ctx.RespondAsync(
                            $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.");
                        return;
                    }
                }

                LavalinkLoadResult results;
                if (query.GetType() == typeof(Uri))
                {
                    results = await node.Rest.GetTracksAsync((Uri) query);
                }
                else
                {
                    results = await node.Rest.GetTracksAsync((string) query);
                }

                if (results.LoadResultType == LavalinkLoadResultType.LoadFailed ||
                    results.LoadResultType == LavalinkLoadResultType.NoMatches)
                {
                    await ctx.RespondAsync($"Search failed for {search}.");
                    return;
                }

                var track = results.Tracks.First();

                var song = track.ToSong(ctx);
                var embed = new DiscordEmbedBuilder
                {
                    Description = $"[{song.Title} ({song.Length})]({song.Url})",
                    Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                    {
                        Url = Gifs[_rand.Next(Gifs.Length)]
                    },
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        IconUrl = ctx.Member.AvatarUrl,
                        Text = $"Requested by {ctx.Member.DisplayName}"
                    }
                };
                if (song.ChannelUrl != null)
                {
                    embed
                        .WithImageUrl(song.ThumbnailUrl)
                        .WithAuthor(song.ChannelName, song.ChannelUrl);
                }

                if (Players[ctx.Guild.Id].Queue.Count == 0)
                {
                    await conn.PlayAsync(track);
                    embed.WithTitle("Now Playing");
                    Players[ctx.Guild.Id].Queue.Add(song);
                    Players[ctx.Guild.Id].CurrentSong = song;
                    await ctx.RespondAsync(embed.Build());
                }
                else
                {
                    if (!ctx.IsBoundChannel(Players[ctx.Guild.Id]))
                    {
                        return;
                    }

                    Players[ctx.Guild.Id].Queue.Add(song);
                    embed.WithTitle("Queued song");
                    await ctx.RespondAsync(embed.Build());
                }
            }
            catch (Exception e)
            {
                ctx.Client.Logger.LogCritical(e.ToString());
            }
        }

        private async Task join_channel(CommandContext ctx, DiscordChannel channel)
        {
            var lavalink = ctx.Client.GetLavalink();
            if (!lavalink.ConnectedNodes.Any())
            {
                await ctx.RespondAsync("An error has occurred, please try again later.");
                ctx.Client.Logger.LogCritical("Lavalink connection is not established!");
                return;
            }

            var node = lavalink.ConnectedNodes.Values.First();

            if (channel.Type is not ChannelType.Voice && channel.Type is not ChannelType.Stage)
            {
                await ctx.RespondAsync("Not a valid voice channel.");
                return;
            }

            await node.ConnectAsync(channel);
            Player player = new()
            {
                Guild = ctx.Guild,
                TextChannel = ctx.Channel,
                VoiceChannel = ctx.Member.VoiceState.Channel
            };
            Players.Add(ctx.Guild.Id, player);
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
            conn.PlaybackFinished += on_track_end;
            await ctx.RespondAsync($"Joined `{channel.Name}` and bound to {ctx.Channel.Mention}!");
        }

        public async Task on_track_end(LavalinkGuildConnection lavalinkGuildConnection, TrackFinishEventArgs e)
        {
            if (Players[lavalinkGuildConnection.Guild.Id].Skipping)
                return;
            var song = new Song();
            try
            {
                song = (e.Track.GetSongByTrack(Players[lavalinkGuildConnection.Guild.Id]));
                Players[lavalinkGuildConnection.Guild.Id].CurrentSong = null;
                if (!Players[lavalinkGuildConnection.Guild.Id].Skipping)
                    await Players[lavalinkGuildConnection.Guild.Id].TextChannel.SendMessageAsync(builder =>
                        builder.WithContent($"Song `{song.Title}` has ended").WithReply(song.MessageId));
                Players[lavalinkGuildConnection.Guild.Id].Skipping = false;
            }
            catch (NullReferenceException)
            {
                lavalinkGuildConnection.Node.Discord.Logger.LogDebug("Track End event errored."); // ik this kinda long but its better to not use static when you can avoid
            }

            if (Players[lavalinkGuildConnection.Guild.Id].Queue.Count > 1)
            {
                try
                {
                    var track = Players[lavalinkGuildConnection.Guild.Id].Queue[Players[lavalinkGuildConnection.Guild.Id].Queue.IndexOf(song) + 1];
                    await lavalinkGuildConnection.PlayAsync(track.Track);
                    Players[lavalinkGuildConnection.Guild.Id].CurrentSong = track;
                    Players[lavalinkGuildConnection.Guild.Id].Queue.Remove(song);
                    if (Players[lavalinkGuildConnection.Guild.Id].Looping)
                        Players[lavalinkGuildConnection.Guild.Id].Queue.Add(song);
                    await Players[lavalinkGuildConnection.Guild.Id].TextChannel.SendMessageAsync(builder =>
                        builder.WithContent($"Now playing: `{track.Title}` added by {track.Requester.Mention}"));
                    return;
                }
                catch
                {
                    await lavalinkGuildConnection.PlayAsync(Players[lavalinkGuildConnection.Guild.Id].Queue[0]
                        .Track);
                    Players[lavalinkGuildConnection.Guild.Id].CurrentSong = Players[lavalinkGuildConnection.Guild.Id].Queue[0];
                    await Players[lavalinkGuildConnection.Guild.Id].TextChannel.SendMessageAsync(builder =>
                        builder.WithContent(
                            $"Now playing: `{Players[lavalinkGuildConnection.Guild.Id].Queue[0].Title}` added by {Players[lavalinkGuildConnection.Guild.Id].Queue[0].Requester.Mention}"));
                    return;
                }
            }
            else
            {
                if (Players[lavalinkGuildConnection.Guild.Id].CurrentSong != null && Players[lavalinkGuildConnection.Guild.Id].Looping)
                {
                    await lavalinkGuildConnection.PlayAsync(Players[lavalinkGuildConnection.Guild.Id].CurrentSong.Track);
                    await Players[lavalinkGuildConnection.Guild.Id].TextChannel.SendMessageAsync(builder =>
                        builder.WithContent(
                            $"Now playing: `{Players[lavalinkGuildConnection.Guild.Id].CurrentSong.Title}` added by {Players[lavalinkGuildConnection.Guild.Id].CurrentSong.Requester.Mention}"));
                    return;
                }
            }

            Players[lavalinkGuildConnection.Guild.Id].Queue.Remove(song);
            Players[lavalinkGuildConnection.Guild.Id].CurrentSong = null;
        }

        [Command("stop"), Description("Clears the queue and disconnects from the voice channel.")]
        private async Task Stop(CommandContext ctx)
        {
            var lavalink = ctx.Client.GetLavalink();
            if (!lavalink.ConnectedNodes.Any())
            {
                await ctx.RespondAsync("An error has occurred, please try again later.");
                ctx.Client.Logger.LogCritical("Lavalink connection is not established!");
                return;
            }

            if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                await ctx.RespondAsync("You are not in a voice channel!");
                return;
            }

            var node = lavalink.ConnectedNodes.Values.First();

            if (ctx.Member.VoiceState.Channel.Type != ChannelType.Voice &&
                ctx.Member.VoiceState.Channel.Type != ChannelType.Stage)
            {
                await ctx.RespondAsync("Not a valid voice channel.");
                return;
            }

            var conn = node.GetGuildConnection(ctx.Guild);

            if (conn == null)
            {
                await ctx.RespondAsync("The bot isn't connected to a voice channel!");
                return;
            }
            else
            {
                if (conn.Channel != ctx.Member.VoiceState.Channel)
                {
                    await ctx.RespondAsync(
                        $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.");
                    return;
                }
            }

            if (!ctx.IsBoundChannel(Players[ctx.Guild.Id]))
                return;

            Players.Remove(ctx.Guild.Id);

            var channel = conn.Channel;
            await conn.DisconnectAsync();
            await ctx.RespondAsync($"Left `{channel.Name}`!");
        }

        [Command("pause"), Description("Pauses the music")]
        public async Task Pause(CommandContext ctx)
        {
            if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                await ctx.RespondAsync("You are not in a voice channel.");
                return;
            }

            var lava = ctx.Client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.RespondAsync("The bot isn't connected to a voice channel!");
                return;
            }

            if (conn.Channel != ctx.Member.VoiceState.Channel)
            {
                await ctx.RespondAsync(
                    $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.");
                return;
            }

            if (!ctx.IsBoundChannel(Players[ctx.Guild.Id]))
                return;

            if (conn.CurrentState.CurrentTrack == null && Players[ctx.Guild.Id].CurrentSong == null)
            {
                await ctx.RespondAsync("Nothing is playing...");
                return;
            }

            if (Players[ctx.Guild.Id].Paused)
            {
                await ctx.RespondAsync("The player is already paused.");
                return;
            }

            Players[ctx.Guild.Id].Paused = true;
            await conn.PauseAsync();
            await ctx.RespondAsync("The player has been paused.");
        }

        [Command("resume"), Description("Resumes the music after pausing.")]
        public async Task Resume(CommandContext ctx)
        {
            if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                await ctx.RespondAsync("You are not in a voice channel.");
                return;
            }

            var lava = ctx.Client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.RespondAsync("The bot isn't connected to a voice channel!");
                return;
            }
            else
            {
                if (conn.Channel != ctx.Member.VoiceState.Channel)
                {
                    await ctx.RespondAsync(
                        $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.");
                    return;
                }
            }

            if (!ctx.IsBoundChannel(Players[ctx.Guild.Id]))
                return;


            if (conn.CurrentState.CurrentTrack == null && Players[ctx.Guild.Id].CurrentSong == null)
            {
                await ctx.RespondAsync("Nothing is playing...");
                return;
            }

            if (!Players[ctx.Guild.Id].Paused)
            {
                await ctx.RespondAsync("The player is not paused.");
                return;
            }

            Players[ctx.Guild.Id].Paused = false;
            await conn.ResumeAsync();
            await ctx.RespondAsync("The player has been resumed if it was paused.");
        }

        [Command("nowplaying"), Description("Shows the currently playing song."), Aliases("np")]
        public async Task NowPlaying(CommandContext ctx)
        {
            if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                await ctx.RespondAsync("You are not in a voice channel.");
                return;
            }

            var lava = ctx.Client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.RespondAsync("The bot isn't connected to a voice channel!");
                return;
            }
            else
            {
                if (conn.Channel != ctx.Member.VoiceState.Channel)
                {
                    await ctx.RespondAsync(
                        $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.");
                    return;
                }
            }

            if (!ctx.IsBoundChannel(Players[ctx.Guild.Id]))
                return;

            ctx.Client.Logger.LogDebug(Players[ctx.Guild.Id].CurrentSong.Title);

            var embed = new DiscordEmbedBuilder
            {
                Title = "Now Playing",
                Description =
                    $"[{Players[ctx.Guild.Id].CurrentSong.Title} ({conn.CurrentState.PlaybackPosition.Hours:00}:{conn.CurrentState.PlaybackPosition.Minutes.ToString("00")}:{conn.CurrentState.PlaybackPosition.Seconds.ToString("00")}/{Players[ctx.Guild.Id].CurrentSong.Length})]({Players[ctx.Guild.Id].CurrentSong.Url})",
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                {
                    Url = Players[ctx.Guild.Id].CurrentSong.ThumbnailUrl
                }
            };
            await ctx.RespondAsync(embed.Build());
        }

        [Command("skip"), Description("Skips the currently playing song."), Aliases("s")]
        public async Task Skip(CommandContext ctx)
        {
            try
            {
                if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
                {
                    await ctx.RespondAsync("You are not in a voice channel.");
                    return;
                }

                var lava = ctx.Client.GetLavalink();
                var node = lava.ConnectedNodes.Values.First();
                var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

                if (conn == null)
                {
                    await ctx.RespondAsync("The bot isn't connected to a voice channel!");
                    return;
                }

                if (conn.Channel != ctx.Member.VoiceState.Channel)
                {
                    await ctx.RespondAsync(
                        $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.");
                    return;
                }

                if (!ctx.IsBoundChannel(Players[ctx.Guild.Id]))
                    return;

                if (conn.CurrentState.CurrentTrack == null || Players[ctx.Guild.Id].CurrentSong == null)
                {
                    await ctx.RespondAsync("Nothing is playing...");
                    return;
                }

                Song song;
                try
                {
                    song = conn.CurrentState.CurrentTrack.GetSongByTrack(Players[ctx.Guild.Id]);
                }
                catch (NullReferenceException)
                {
                    song = Players[ctx.Guild.Id].CurrentSong;
                }

                await ctx.RespondAsync($"Skipping `{song.Title}`");
                Players[ctx.Guild.Id].Skipping = true;

                if (Players[ctx.Guild.Id].Queue.Count > 1)
                {
                    try
                    {
                        var track = Players[ctx.Guild.Id].Queue[Players[ctx.Guild.Id].Queue.IndexOf(song) + 1];
                        await conn.StopAsync();
                        await conn.PlayAsync(track.Track);
                        Players[ctx.Guild.Id].CurrentSong = track;
                        Players[ctx.Guild.Id].Queue.Remove(song);
                        if (Players[ctx.Guild.Id].Looping)
                            Players[ctx.Guild.Id].Queue.Add(song);
                        await Players[ctx.Guild.Id].TextChannel.SendMessageAsync(builder =>
                            builder.WithContent($"Now playing: `{track.Title}` added by {track.Requester.Mention}"));
                        return;
                    }
                    catch
                    {
                        await conn.PlayAsync(Players[ctx.Guild.Id].Queue[0]
                            .Track);
                        Players[ctx.Guild.Id].Queue.Remove(song);
                        if (Players[ctx.Guild.Id].Looping)
                            Players[ctx.Guild.Id].Queue.Add(song);
                        Players[ctx.Guild.Id].CurrentSong = Players[ctx.Guild.Id].Queue[0];
                        await Players[ctx.Guild.Id].TextChannel.SendMessageAsync(builder =>
                            builder.WithContent(
                                $"Now playing: `{Players[ctx.Guild.Id].Queue[0].Title}` added by {Players[ctx.Guild.Id].Queue[0].Requester.Mention}"));
                        return;
                    }
                }

                Players[ctx.Guild.Id].Queue.Remove(song);
                Players[ctx.Guild.Id].CurrentSong = null;
                await conn.StopAsync();
            }
            catch (Exception e)
            {
                ctx.Client.Logger.LogCritical(e.ToString());
            }
        }

        [Command("clear"), Description("Clears the queue.")]
        public async Task Clear(CommandContext ctx)
        {
            if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                await ctx.RespondAsync("You are not in a voice channel.");
                return;
            }

            var lava = ctx.Client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.RespondAsync("The bot isn't connected to a voice channel!");
                return;
            }
            else
            {
                if (conn.Channel != ctx.Member.VoiceState.Channel)
                {
                    await ctx.RespondAsync(
                        $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.");
                    return;
                }
            }

            if (!ctx.IsBoundChannel(Players[ctx.Guild.Id]))
                return;

            Players[ctx.Guild.Id].Queue = new();
            await ctx.RespondAsync("The queue has been cleared.");
        }

        [Command("queue"), Description("Shows the current queue"), Aliases("q")]
        public async Task Queue(CommandContext ctx)
        {
            if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                await ctx.RespondAsync("You are not in a voice channel.");
                return;
            }

            var lava = ctx.Client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.RespondAsync("The bot isn't connected to a voice channel!");
                return;
            }
            else
            {
                if (conn.Channel != ctx.Member.VoiceState.Channel)
                {
                    await ctx.RespondAsync(
                        $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.");
                    return;
                }
            }

            if (!ctx.IsBoundChannel(Players[ctx.Guild.Id]))
                return;

            var embed = new DiscordEmbedBuilder
            {
                Title = "Queue",
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                {
                    Url = Gifs[_rand.Next(Gifs.Length)]
                }
            };

            var desc = "";
            if (Players[ctx.Guild.Id].Queue.Count <= 1)
            {
                embed.WithDescription("There are no songs in the queue. Use $play to add songs!");
                await ctx.RespondAsync(embed.Build());
            }
            else
            {
                desc +=
                    $"**Now playing - `{Players[ctx.Guild.Id].Queue[0].Title}` added by {Players[ctx.Guild.Id].Queue[0].Requester.Mention}**\n\n";

                for (var i = 1; i < Players[ctx.Guild.Id].Queue.Count; i++)
                {
                    desc +=
                        $"{i} - `{Players[ctx.Guild.Id].Queue[i].Title}` added by {Players[ctx.Guild.Id].Queue[i].Requester.Mention}\n";
                }

                var interact = ctx.Client.GetInteractivity();
                var pages = interact.GeneratePagesInEmbed(desc, SplitType.Line, embed);
                try
                {
                    await ctx.Channel.SendPaginatedMessageAsync(ctx.Member, pages, new PaginationButtons());
                }
                catch (Exception e)
                {
                    ctx.Client.Logger.LogCritical(e.ToString());
                }

                return;
            }
        }

        [Command("remove"), Description("Removes a song from the queue, get the song's index from $queue.")]
        public async Task Remove(CommandContext ctx,
            [Description("The index of the song to be removed from the queue.")]
            int index)
        {
            if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                await ctx.RespondAsync("You are not in a voice channel.");
                return;
            }

            var lava = ctx.Client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.RespondAsync("The bot isn't connected to a voice channel!");
                return;
            }

            if (conn.Channel != ctx.Member.VoiceState.Channel)
            {
                await ctx.RespondAsync(
                    $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.");
                return;
            }

            if (!ctx.IsBoundChannel(Players[ctx.Guild.Id]))
                return;

            if (Players[ctx.Guild.Id].Queue.Count <= 1)
            {
                await ctx.RespondAsync("There are no songs in the queue.");
                return;
            }

            if ((Players[ctx.Guild.Id].Queue.Count >= index + 1) == false)
            {
                await ctx.RespondAsync(
                    $"There's no song with that index, please use `{Singer.Config.Prefixes}queue`!");
                return;
            }

            var song = Players[ctx.Guild.Id].Queue[index];
            Players[ctx.Guild.Id].Queue.Remove(song);
            await ctx.RespondAsync($"Removed song `{song.Title}`");
        }

        [Command("loop"), Description("Loops the queue")]
        public async Task Loop(CommandContext ctx)
        {
            if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                await ctx.RespondAsync("You are not in a voice channel.");
                return;
            }
            
            var lava = ctx.Client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.RespondAsync("The bot isn't connected to a voice channel!");
                return;
            }
            else
            {
                if (conn.Channel != ctx.Member.VoiceState.Channel)
                {
                    await ctx.RespondAsync(
                        $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.");
                    return;
                }
            }

            if (!ctx.IsBoundChannel(Players[ctx.Guild.Id]))
                return;

            if (Players[ctx.Guild.Id].Looping)
            {
                Players[ctx.Guild.Id].Looping = false;
                await ctx.RespondAsync("Looping has been disabled!");
                return;
            }

            Players[ctx.Guild.Id].Looping = true;
            await ctx.RespondAsync("Looping has been enabled!");
        }

        [Command("shuffle"), Description("Shuffles the queue")]
        public async Task Shuffle(CommandContext ctx)
        {
            // DO the usual checks
            if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                await ctx.RespondAsync("You are not in a voice channel.");
                return;
            }
            
            var lava = ctx.Client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
            
            if (conn == null)
            {
                await ctx.RespondAsync("The bot isn't connected to a voice channel!");
                return;
            }
            else
            {
                if (conn.Channel != ctx.Member.VoiceState.Channel)
                {
                    await ctx.RespondAsync(
                        $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.");
                    return;
                }
            }
            
            if (!ctx.IsBoundChannel(Players[ctx.Guild.Id]))
                return;
            
            // Shuffle the queue
            Players[ctx.Guild.Id].Queue.Shuffle();
            await ctx.RespondAsync("The queue has been shuffled!");
        }
    }
}
























