using System;
using System.Linq;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using DSharpPlus.SlashCommands;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Singer.Commands;

namespace Singer.Constructs
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
        
        private static YouTubeService _youtubeService;

        public static YouTubeService YoutubeService
        {
            // ik i said that listen to rider's suggestion, but dont do that here, doing so will make different youtube service everytime you use this variable
            get => _youtubeService ??= new YouTubeService(new BaseClientService.Initializer
            {
                ApiKey = Singer.Config.YoutubeApiKey
            });
        }
    }
}