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
	private static readonly string TITLE = "Sus Music TM 3.2";

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
		var nowPlayingEmbed = new DiscordEmbedBuilder()
		{
			Title = TITLE,
			Description = "Queue music by sending a url or title",
		};


		if (player.CurrentTrack != null)  // Something is in queue
		{
			string queue = "Queue:";

			for (int i = 0; i < player.Queue.Count; i++)
			{
				queue += $"\n{i}: {player.Queue.Skip(i).First().Track.Title} - {player.Queue.Skip(i).First().Track.Author}";
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
			};
	}

}
