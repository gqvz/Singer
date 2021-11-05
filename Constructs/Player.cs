using System.Collections.Generic;
using DSharpPlus.Entities;

namespace Singer.Constructs
{
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