using System;
using System.Collections.Generic;
using System.Linq;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using DSharpPlus.SlashCommands;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

namespace Singer
{
    public class Song
    {
        public string Title { get; set; }
        public string ChannelName { get; set; }
        public string ChannelId { get; set; }
        public string ChannelUrl { get; set; }
        public string VideoId { get; set; }
        public Uri Url { get; set; }
        public string ThumbnailUrl { get; set; }
        public TimeSpan Length { get; set; }
        public DiscordUser Requester { get; set; }
        public LavalinkTrack Track { get; set; }
        public ulong MessageId { get; set; } = UInt64.MaxValue;
        public InteractionContext Interaction { get; set; } = null;

        private static YouTubeService _youtubeService = new(new BaseClientService.Initializer()
        {
            ApiKey = Config.ApiKey
        });

        public static Song ToSong(LavalinkTrack track, CommandContext ctx)
        {
            var searchRequest = _youtubeService.Videos.List("snippet");
            searchRequest.Id = track.Identifier;
            var response = searchRequest.Execute();
            var video = response.Items.FirstOrDefault();

            Song song;
            if (video != null)
            {
                song = new Song()
                {
                    Title = track.Title,
                    VideoId = track.Identifier,
                    Url = track.Uri,
                    ChannelId = video.Snippet.ChannelId,
                    ChannelName = video.Snippet.ChannelTitle,
                    ChannelUrl = "https://youtube.com/channel/" + video.Snippet.ChannelId,
                    ThumbnailUrl = video.Snippet.Thumbnails.High.Url,
                    Length = track.Length,
                    Requester = ctx.Member,
                    Track = track,
                    MessageId = ctx.Message.Id
                };
            }
            else
            {
                song = new Song()
                {
                    Title = track.Title,
                    VideoId = track.Identifier,
                    Url = track.Uri,
                    Length = track.Length,
                    Requester = ctx.Member,
                    Track = track,
                    MessageId = ctx.Message.Id
                };
            }
            return song;
        }
        
        public static Song ToSong(LavalinkTrack track, InteractionContext ctx)
        {
            var searchRequest = _youtubeService.Videos.List("snippet");
            searchRequest.Id = track.Identifier;
            var response = searchRequest.Execute();
            var video = response.Items.FirstOrDefault();

            Song song;
            if (video != null)
            {
                song = new Song()
                {
                    Title = track.Title,
                    VideoId = track.Identifier,
                    Url = track.Uri,
                    ChannelId = video.Snippet.ChannelId,
                    ChannelName = video.Snippet.ChannelTitle,
                    ChannelUrl = "https://youtube.com/channel/" + video.Snippet.ChannelId,
                    ThumbnailUrl = video.Snippet.Thumbnails.High.Url,
                    Length = track.Length,
                    Requester = ctx.Member,
                    Track = track,
                    Interaction = ctx
                };
            }
            else
            {
                song = new Song()
                {
                    Title = track.Title,
                    VideoId = track.Identifier,
                    Url = track.Uri,
                    Length = track.Length,
                    Requester = ctx.Member,
                    Track = track,
                    Interaction = ctx
                };
            }
            return song;
        }

        public static Song GetSongByTrack(LavalinkTrack track, Player player)
        {
            foreach (var song in player.Queue.Where(song => song.Track.Uri == track.Uri))
            {
                return song;
            }

            return null;
        }
    }

    public class Player
    {
        public DiscordGuild Guild { get; set; }
        public DiscordChannel TextChannel { get; set; }
        public DiscordChannel VoiceChannel { get; set; }
        public List<Song> Queue { get; set; } = new();
        public Song CurrentSong { get; set; }
        public bool Paused { get; set; } = false;
        public bool Looping { get; set; } = false;
        public bool Skipping { get; set; } = false;
    }
}