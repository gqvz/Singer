using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Lavalink;
using DSharpPlus.Lavalink.EventArgs;
using DSharpPlus.SlashCommands;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.EventHandling;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.Extensions.Logging;
using Singer.Constructs;

namespace Singer.Commands
{
    public class SlashTest : ApplicationCommandModule
    {
        private readonly Random _rand = new();

        [SlashCommand("ping", "Returns the latency between the bot and Discord.")]
        public async Task Ping(InteractionContext ctx)
        {
            var embed = new DiscordEmbedBuilder
            {
                Title = "Pong! 🏓",
                Description = $"Latency: {ctx.Client.Ping}ms",
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    IconUrl = ctx.Member.AvatarUrl,
                    Text = $"Requested by {ctx.Member.Username}"
                }
            };
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AddEmbed(embed.Build()));
        }

        [SlashCommand("play", "Plays a song. Enter the song's name or URL!")]
        public async Task Play(InteractionContext ctx,
            [Option("Song", "The name/URL of the song to be played")]
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
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("You are not in a voice channel!")
                            .AsEphemeral(true));
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
                        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder()
                                .WithContent(
                                    $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.")
                                .AsEphemeral(true));
                        return;
                    }
                }

                try
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent(
                            "\\**insert the greatest music search montage**"));
                }
                catch (NotFoundException)
                {
                    await ctx.EditResponseAsync(
                        new DiscordWebhookBuilder().WithContent("\\**insert the greatest music search montage**"));
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
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Search failed for {search}"));
                }

                var track = results.Tracks.First();

                var song = track.ToSong(ctx);
                var embed = new DiscordEmbedBuilder
                {
                    Description = $"[{song.Title} ({song.Length})]({song.Url})",
                    Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                    {
                        Url = GeneralCommands.Gifs[_rand.Next(GeneralCommands.Gifs.Length)]
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

                if (GeneralCommands.Players[ctx.Guild.Id].Queue.Count == 0)
                {
                    await conn.PlayAsync(track);
                    embed.WithTitle("Now Playing");
                    GeneralCommands.Players[ctx.Guild.Id].Queue.Add(song);
                    GeneralCommands.Players[ctx.Guild.Id].CurrentSong = song;
                }
                else
                {
                    if (!ctx.IsBoundChannel(GeneralCommands.Players[ctx.Guild.Id]))
                    {
                        return;
                    }

                    GeneralCommands.Players[ctx.Guild.Id].Queue.Add(song);
                    embed.WithTitle("Queued song");
                }

                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed.Build()));
            }
            catch (Exception e)
            {
                ctx.Client.Logger.LogCritical(e.ToString());
            }
        }

        private async Task join_channel(InteractionContext ctx, DiscordChannel channel)
        {
            var lavalink = ctx.Client.GetLavalink();
            if (!lavalink.ConnectedNodes.Any())
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("An error has occurred, please try again later.").AsEphemeral(true));
                ctx.Client.Logger.LogCritical("Lavalink connection is not established!");
                return;
            }

            var node = lavalink.ConnectedNodes.Values.First();

            if (channel.Type is not ChannelType.Voice && channel.Type is not ChannelType.Stage)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Not a valid voice channel.")
                        .AsEphemeral(true));
                return;
            }

            await node.ConnectAsync(channel);
            Player player = new()
            {
                Guild = ctx.Guild,
                TextChannel = ctx.Channel,
                VoiceChannel = ctx.Member.VoiceState.Channel
            };
            GeneralCommands.Players.Add(ctx.Guild.Id, player);
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
            conn.PlaybackFinished += on_track_end;
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent(
                    $"Joined `{channel.Name}` and bound to {ctx.Channel.Mention}!"));
        }

        public async Task on_track_end(LavalinkGuildConnection lavalinkGuildConnection, TrackFinishEventArgs e)
        {
            if (GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].Skipping)
                return;
            var song = new Song();
            try
            {
                song = e.Track.GetSongByTrack(GeneralCommands.Players[lavalinkGuildConnection.Guild.Id]);
                GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].CurrentSong = null;
                if (!GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].Skipping)
                    await GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].TextChannel
                        .SendMessageAsync(builder => builder.WithContent($"Song `{song.Title}` has ended"));
                GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].Skipping = false;
            }
            catch (NullReferenceException)
            {
                lavalinkGuildConnection.Node.Discord.Logger.LogCritical("Track End event errored.");
            }

            if (GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].Queue.Count > 1)
            {
                try
                {
                    var track = GeneralCommands.Players[lavalinkGuildConnection.Guild.Id]
                        .Queue[GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].Queue.IndexOf(song) + 1];
                    await lavalinkGuildConnection.PlayAsync(track.Track);
                    GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].CurrentSong = track;
                    GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].Queue.Remove(song);
                    if (GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].Looping)
                        GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].Queue.Add(song);
                    await GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].TextChannel.SendMessageAsync(builder =>
                        builder.WithContent($"Now playing: `{track.Title}` added by {track.Requester.Mention}"));
                    return;
                }
                catch
                {
                    await lavalinkGuildConnection.PlayAsync(GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].Queue[0]
                        .Track);
                    GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].CurrentSong =
                        GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].Queue[0];
                    await GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].TextChannel.SendMessageAsync(builder =>
                        builder.WithContent(
                            $"Now playing: `{GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].Queue[0].Title}` added by {GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].Queue[0].Requester.Mention}"));
                    return;
                }
            }
            else
            {
                if (GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].CurrentSong != null && GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].Looping)
                {
                    await lavalinkGuildConnection.PlayAsync(GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].CurrentSong.Track);
                    await GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].TextChannel.SendMessageAsync(builder =>
                        builder.WithContent(
                            $"Now playing: `{GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].CurrentSong.Title}` added by {GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].CurrentSong.Requester.Mention}"));
                    return;
                }
            }

            GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].Queue.Remove(song);
            GeneralCommands.Players[lavalinkGuildConnection.Guild.Id].CurrentSong = null;
        }

        [SlashCommand("stop", "Clears the queue and disconnects from the voice channel.")]
        public async Task Stop(InteractionContext ctx)
        {
            var lavalink = ctx.Client.GetLavalink();
            if (!lavalink.ConnectedNodes.Any())
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("An error has occurred, please try again later.").AsEphemeral(true));
                ctx.Client.Logger.LogCritical("Lavalink connection is not established!");
                return;
            }

            var node = lavalink.ConnectedNodes.Values.First();

            if (ctx.Member.VoiceState.Channel.Type != ChannelType.Voice &&
                ctx.Member.VoiceState.Channel.Type != ChannelType.Stage)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Not a valid voice channel.")
                        .AsEphemeral(true));
                return;
            }

            var conn = node.GetGuildConnection(ctx.Guild);

            if (conn == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("The bot isn't connected to a voice channel!")
                        .AsEphemeral(true));
                return;
            }
            else
            {
                if (conn.Channel != ctx.Member.VoiceState.Channel)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent(
                                $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.")
                            .AsEphemeral(true));
                    return;
                }
            }

            if (!ctx.IsBoundChannel(GeneralCommands.Players[ctx.Guild.Id]))
                return;

            GeneralCommands.Players.Remove(ctx.Guild.Id);

            var channel = conn.Channel;
            await conn.DisconnectAsync();
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent($"Left `{channel.Name}`"));
        }

        [SlashCommand("pause", "Pauses the music")]
        public async Task Pause(InteractionContext ctx)
        {
            if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You are not in a voice channel.")
                        .AsEphemeral(true));
                return;
            }

            var lava = ctx.Client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("The bot isn't connected to a voice channel!")
                        .AsEphemeral(true));
                return;
            }

            if (conn.Channel != ctx.Member.VoiceState.Channel)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent(
                            $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.")
                        .AsEphemeral(true));
                return;
            }

            if (!ctx.IsBoundChannel(GeneralCommands.Players[ctx.Guild.Id]))
                return;

            if (conn.CurrentState.CurrentTrack == null && GeneralCommands.Players[ctx.Guild.Id].CurrentSong == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Nothing is playing...").AsEphemeral(true));
                return;
            }

            if (GeneralCommands.Players[ctx.Guild.Id].Paused)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("The player is already paused.")
                        .AsEphemeral(true));
                return;
            }

            GeneralCommands.Players[ctx.Guild.Id].Paused = true;
            await conn.PauseAsync();
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("The player has been paused."));
        }

        [SlashCommand("resume", "Resumes the music after pausing.")]
        public async Task Resume(InteractionContext ctx)
        {
            if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You are not in a voice channel.")
                        .AsEphemeral(true));
                return;
            }

            var lava = ctx.Client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("The bot isn't connected to a voice channel!")
                        .AsEphemeral(true));
                return;
            }

            if (conn.Channel != ctx.Member.VoiceState.Channel)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent(
                            $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.")
                        .AsEphemeral(true));
                return;
            }

            if (!ctx.IsBoundChannel(GeneralCommands.Players[ctx.Guild.Id]))
                return;


            if (conn.CurrentState.CurrentTrack == null && GeneralCommands.Players[ctx.Guild.Id].CurrentSong == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Nothing is playing...").AsEphemeral(true));
                return;
            }

            if (!GeneralCommands.Players[ctx.Guild.Id].Paused)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("The player is not paused.").AsEphemeral(true));
                return;
            }

            GeneralCommands.Players[ctx.Guild.Id].Paused = false;
            await conn.ResumeAsync();
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("The player has resumed playing."));
        }

        [SlashCommand("nowplaying", "Shows the currently playing song.")]
        public async Task NowPlaying(InteractionContext ctx)
        {
            if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You are not in a voice channel.")
                        .AsEphemeral(true));
                return;
            }

            var lava = ctx.Client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("The bot isn't connected to a voice channel!")
                        .AsEphemeral(true));
                return;
            }

            if (conn.Channel != ctx.Member.VoiceState.Channel)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent(
                            $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.")
                        .AsEphemeral(true));
                return;
            }

            if (!ctx.IsBoundChannel(GeneralCommands.Players[ctx.Guild.Id]))
                return;

            var embed = new DiscordEmbedBuilder
            {
                Title = "Now Playing",
                Description =
                    $"[{GeneralCommands.Players[ctx.Guild.Id].CurrentSong.Title} ({conn.CurrentState.PlaybackPosition.Hours.ToString("00")}:{conn.CurrentState.PlaybackPosition.Minutes.ToString("00")}:{conn.CurrentState.PlaybackPosition.Seconds.ToString("00")}/{GeneralCommands.Players[ctx.Guild.Id].CurrentSong.Length})]({GeneralCommands.Players[ctx.Guild.Id].CurrentSong.Url})",
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                {
                    Url = GeneralCommands.Players[ctx.Guild.Id].CurrentSong.ThumbnailUrl
                }
            };
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AddEmbed(embed.Build()));
        }

        [SlashCommand("skip", "Skips the currently playing song.")]
        public async Task Skip(InteractionContext ctx)
        {
            try
            {
                if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("You are not in a voice channel.")
                            .AsEphemeral(true));
                    return;
                }

                var lava = ctx.Client.GetLavalink();
                var node = lava.ConnectedNodes.Values.First();
                var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

                if (conn == null)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("The bot isn't connected to a voice channel!").AsEphemeral(true));
                    return;
                }

                if (conn.Channel != ctx.Member.VoiceState.Channel)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent(
                                $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.")
                            .AsEphemeral(true));
                    return;
                }

                if (!ctx.IsBoundChannel(GeneralCommands.Players[ctx.Guild.Id]))
                    return;

                if (conn.CurrentState.CurrentTrack == null && GeneralCommands.Players[ctx.Guild.Id].CurrentSong == null)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Nothing is playing...").AsEphemeral(true));
                    return;
                }

                Song song;
                try
                {
                    song = conn.CurrentState.CurrentTrack.GetSongByTrack(GeneralCommands.Players[ctx.Guild.Id]);
                }
                catch (NullReferenceException)
                {
                    song = GeneralCommands.Players[ctx.Guild.Id].CurrentSong;
                }

                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"Skipping `{song.Title}`"));
                GeneralCommands.Players[ctx.Guild.Id].Skipping = true;

                if (GeneralCommands.Players[ctx.Guild.Id].Queue.Count > 1)
                {
                    try
                    {
                        var track = GeneralCommands.Players[ctx.Guild.Id]
                            .Queue[GeneralCommands.Players[ctx.Guild.Id].Queue.IndexOf(song) + 1];
                        await conn.StopAsync();
                        await conn.PlayAsync(track.Track);
                        GeneralCommands.Players[ctx.Guild.Id].CurrentSong = track;
                        GeneralCommands.Players[ctx.Guild.Id].Queue.Remove(song);
                        if (GeneralCommands.Players[ctx.Guild.Id].Looping)
                            GeneralCommands.Players[ctx.Guild.Id].Queue.Add(song);
                        await GeneralCommands.Players[ctx.Guild.Id].TextChannel.SendMessageAsync(builder =>
                            builder.WithContent($"Now playing: `{track.Title}` added by {track.Requester.Mention}"));
                        return;
                    }
                    catch
                    {
                        await conn.PlayAsync(GeneralCommands.Players[ctx.Guild.Id].Queue[0]
                            .Track);
                        GeneralCommands.Players[ctx.Guild.Id].Queue.Remove(song);
                        if (GeneralCommands.Players[ctx.Guild.Id].Looping)
                            GeneralCommands.Players[ctx.Guild.Id].Queue.Add(song);
                        GeneralCommands.Players[ctx.Guild.Id].CurrentSong =
                            GeneralCommands.Players[ctx.Guild.Id].Queue[0];
                        await GeneralCommands.Players[ctx.Guild.Id].TextChannel.SendMessageAsync(builder =>
                            builder.WithContent(
                                $"Now playing: `{GeneralCommands.Players[ctx.Guild.Id].Queue[0].Title}` added by {GeneralCommands.Players[ctx.Guild.Id].Queue[0].Requester.Mention}"));
                        return;
                    }
                }

                GeneralCommands.Players[ctx.Guild.Id].Queue.Remove(song);
                GeneralCommands.Players[ctx.Guild.Id].CurrentSong = null;
                await conn.StopAsync();
            }
            catch (Exception e)
            {
                ctx.Client.Logger.LogCritical(e.ToString());
            }
        }

        [SlashCommand("clear", "Clears the queue.")]
        public async Task Clear(InteractionContext ctx)
        {
            if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You are not in a voice channel.")
                        .AsEphemeral(true));
                return;
            }

            var lava = ctx.Client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("The bot isn't connected to a voice channel!")
                        .AsEphemeral(true));
                return;
            }

            if (conn.Channel != ctx.Member.VoiceState.Channel)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent(
                            $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.")
                        .AsEphemeral(true));
                return;
            }

            if (!ctx.IsBoundChannel(GeneralCommands.Players[ctx.Guild.Id]))
                return;

            GeneralCommands.Players[ctx.Guild.Id].Queue = new();
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("The queue has been cleared."));
        }

        [SlashCommand("queue", "Shows the current queue")]
        public async Task Queue(InteractionContext ctx)
        {
            if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You are not in a voice channel.")
                        .AsEphemeral(true));
                return;
            }

            var lava = ctx.Client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("The bot isn't connected to a voice channel!")
                        .AsEphemeral(true));
                return;
            }
            else
            {
                if (conn.Channel != ctx.Member.VoiceState.Channel)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent(
                                $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.")
                            .AsEphemeral(true));
                    return;
                }
            }

            if (!ctx.IsBoundChannel(GeneralCommands.Players[ctx.Guild.Id]))
                return;

            var embed = new DiscordEmbedBuilder
            {
                Title = "Queue",
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                {
                    Url = GeneralCommands.Gifs[_rand.Next(GeneralCommands.Gifs.Length)]
                }
            };

            var desc = "";
            if (GeneralCommands.Players[ctx.Guild.Id].Queue.Count <= 1)
            {
                embed.WithDescription("There are no songs in the queue. Use $play to add songs!");
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().AddEmbed(embed.Build()));
            }
            else
            {
                desc +=
                    $"**Now playing - `{GeneralCommands.Players[ctx.Guild.Id].Queue[0].Title}` added by {GeneralCommands.Players[ctx.Guild.Id].Queue[0].Requester.Mention}**\n\n";

                for (var i = 1; i < GeneralCommands.Players[ctx.Guild.Id].Queue.Count; i++)
                {
                    desc +=
                        $"{i} - `{GeneralCommands.Players[ctx.Guild.Id].Queue[i].Title}` added by {GeneralCommands.Players[ctx.Guild.Id].Queue[i].Requester.Mention}\n";
                }

                var interact = ctx.Client.GetInteractivity();
                var pages = interact.GeneratePagesInEmbed(desc, SplitType.Line, embed);
                await ctx.Interaction.SendPaginatedResponseAsync(false, ctx.Member, pages, new PaginationButtons());
            }
        }

        [SlashCommand("remove", "Removes a song from the queue, get the song's index from $queue.")]
        public async Task Remove(InteractionContext ctx,
            [Option("Index", "The index of the song to be removed from the queue.")]
            string ind)
        {
            int index;
            try
            {
                index = Convert.ToInt32(ind);
            }
            catch (Exception)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("The index must be a number!")
                        .AsEphemeral(true));
                return;
            }

            if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You are not in a voice channel.")
                        .AsEphemeral(true));
                return;
            }

            var lava = ctx.Client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("The bot isn't connected to a voice channel!")
                        .AsEphemeral(true));
                return;
            }

            if (conn.Channel != ctx.Member.VoiceState.Channel)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent(
                            $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.")
                        .AsEphemeral(true));
                return;
            }


            if (!ctx.IsBoundChannel(GeneralCommands.Players[ctx.Guild.Id]))
                return;

            if (GeneralCommands.Players[ctx.Guild.Id].Queue.Count <= 1)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("There are no songs in the queue."));
                return;
            }

            if ((GeneralCommands.Players[ctx.Guild.Id].Queue.Count >= index + 1) == false)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent(
                        $"There's no song with that index, please use `{Singer.Config.Prefixes.ToList()[0]}queue`!"));
                return;
            }

            var song = GeneralCommands.Players[ctx.Guild.Id].Queue[index];
            GeneralCommands.Players[ctx.Guild.Id].Queue.Remove(song);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent($"Removed song `{song.Title}`"));
        }
        
        [SlashCommand("loop", "Loops the queue")]
        public async Task Loop(InteractionContext ctx)
        {
            if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You're not in a voice channel!")
                        .AsEphemeral(true));
                return;
            }
            
            var lava = ctx.Client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("The bot isn't connected to a voice channel!")
                        .AsEphemeral(true));
            }
            else
            {
                if (conn.Channel != ctx.Member.VoiceState.Channel)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent(
                                $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.")
                            .AsEphemeral(true));
                }
            }

            if (!ctx.IsBoundChannel(GeneralCommands.Players[ctx.Guild.Id]))
                return;

            if (GeneralCommands.Players[ctx.Guild.Id].Looping)
            {
                GeneralCommands.Players[ctx.Guild.Id].Looping = false;
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Looping has been disabled!"));
                return;
            }

            GeneralCommands.Players[ctx.Guild.Id].Looping = true;
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("Looping has been disabled"));
        }

        [SlashCommand("shuffle", "Shuffles the queue")]
        public async Task Shuffle(InteractionContext ctx)
        {
            // Do the usual checks
            if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You're not in a voice channel!")
                        .AsEphemeral(true));
                return;
            }
            
            var lava = ctx.Client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
            
            if (conn == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("The bot isn't connected to a voice channel!")
                        .AsEphemeral(true));
            }
            else
            {
                if (conn.Channel != ctx.Member.VoiceState.Channel)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent(
                                $"The bot is bound to {conn.Channel.Mention}. Please move to that channel or disconnect the bot from that channel.")
                            .AsEphemeral(true));
                }
            }
            
            if (!ctx.IsBoundChannel(GeneralCommands.Players[ctx.Guild.Id]))
                return;
            
            // Shuffle queue
            GeneralCommands.Players[ctx.Guild.Id].Queue.Shuffle();
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("The queue has been shuffled!"));
        }
    }
}