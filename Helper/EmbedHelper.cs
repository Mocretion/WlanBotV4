using DSharpPlus;
using DSharpPlus.Entities;
using Lavalink4NET.Players.Queued;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class EmbedHelper
{
	private static readonly string TITLE = "Sus Music TM 4.2";

	/// <summary>
	/// Gets the thumbnail url of a youtube video.
	/// </summary>
	/// <param name="url"> the url of the video </param>
	/// <param name="size"> the size of the thumbnail, 0 - 3 </param>
	/// <returns> the thumbnails url </returns>
	public static string GetYouTubeThumbnail(string url, byte type = 0)
	{
		if (type < 0) type = 0;
		if (type > 3) type = 3;

		return $"https://img.youtube.com/vi/{url.Substring(url.IndexOf("=") + 1)}/{type}.jpg";
	}

	public static DiscordEmbed GenerateEmbed()
	{
		var nowPlayingEmbed = new DiscordEmbedBuilder()
		{
			Title = TITLE,
			Description = "Queue music by sending a url or title",
			Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ"  // Rick Roll
		};

		nowPlayingEmbed.AddField("Nothing is playing.", "The queue is empty.")
				.WithFooter("Made by Mocretion")
				.WithImageUrl("https://c.tenor.com/CZd0MAcCnNcAAAAC/among-us-amogus.gif")
				.WithTitle(TITLE)
				.WithColor(DiscordColor.Red);

		DiscordEmbed embed = nowPlayingEmbed.Build();

		return embed;
	}

	public static DiscordEmbed GenerateEmbed(QueuedLavalinkPlayer player)
	{
		const int MaxFieldLength = 1024;
		const string MoreSuffix = "\n... and {0} more";

		var nowPlayingEmbed = new DiscordEmbedBuilder()
		{
			Title = TITLE,
			Description = "Queue music by sending a url or title",
		};


		if (player.CurrentTrack != null)  // Something is in queue
		{
			string queue = "Queue:";
			int displayedCount = 0;
			int totalCount = player.Queue.Count;

			foreach (var (item, index) in player.Queue.Select((item, i) => (item, i)))
			{
				string entry = $"\n{index}: {item.Track.Title} - {item.Track.Author}";

				// Reserve space for "... and X more" suffix (estimate max digits needed)
				int reservedSpace = string.Format(MoreSuffix, totalCount).Length;

				if (queue.Length + entry.Length + reservedSpace > MaxFieldLength)
				{
					int remaining = totalCount - displayedCount;
					queue += string.Format(MoreSuffix, remaining);
					break;
				}

				queue += entry;
				displayedCount++;
			}

			if (queue == "Queue:")
				queue = "The queue is empty.";

			nowPlayingEmbed.AddField($"{player.CurrentTrack.Title} - {player.CurrentTrack.Author} - {player.CurrentTrack.Duration}", queue)
				.WithAuthor("SusBot")
				.WithFooter("Made by Mocretion")
				.WithImageUrl(GetYouTubeThumbnail(player.CurrentTrack.Uri.ToString(), 0))
				.WithUrl(player.CurrentTrack.Uri);

			if (!player.IsPaused)
			{
				if (player.RepeatMode == TrackRepeatMode.Track)
					nowPlayingEmbed.WithTitle(TITLE + " 🔂").WithColor(DiscordColor.SpringGreen);
				else
					nowPlayingEmbed.WithTitle(TITLE).WithColor(DiscordColor.CornflowerBlue);
			}
			else
			{
				if (player.RepeatMode == TrackRepeatMode.Track)
					nowPlayingEmbed.WithTitle(TITLE + " ⏸️🔂").WithColor(DiscordColor.Gray);
				else
					nowPlayingEmbed.WithTitle(TITLE + " ⏸️").WithColor(DiscordColor.Gray);
			}
		}
		else  // Nothing is in queue
		{
			nowPlayingEmbed.AddField("Nothing is playing.", "The queue is empty.")
				.WithFooter("Made by Mocretion")
				.WithImageUrl("https://c.tenor.com/CZd0MAcCnNcAAAAC/among-us-amogus.gif")
				.WithColor(DiscordColor.Red)
				.WithUrl("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
		}
		DiscordEmbed embed = nowPlayingEmbed.Build();

		return embed;
	}

	public static DiscordComponent[] GenerateButtonComponents()
	{
		return new DiscordComponent[]
			{
					new DiscordButtonComponent(ButtonStyle.Danger, $"0", "Stop"),
					new DiscordButtonComponent(ButtonStyle.Primary, $"1", "Skip"),
					new DiscordButtonComponent(ButtonStyle.Secondary, $"2", "Pause"),
					new DiscordButtonComponent(ButtonStyle.Success, $"3", "Loop"),
					new DiscordButtonComponent(ButtonStyle.Primary, $"4", "Join"),
			};
	}

}
