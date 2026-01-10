using DSharpPlus.EventArgs;
using Lavalink4NET.Players.Queued;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

public static class EmbedInteractions
{
	private static readonly HttpClient _httpClient = new();

	public static async Task HandleButtonAsync(string buttonId, CustomQueuedPlayer player, ComponentInteractionCreateEventArgs args)
	{
		switch (buttonId)
		{
			case "0":  // Stop
				await player.Queue.ClearAsync();
				player.RepeatMode = TrackRepeatMode.None;
				await player.StopAsync();
				await player.UpdateEmbedAsync(args.Channel);
				break;

			case "1":  // Skip
				if (player.Queue.Count == 0)
				{
					await player.StopAsync();
				}
				else
				{
					await player.SkipAsync();
				}
				await player.UpdateEmbedAsync(args.Channel);
				break;

			case "2":  // Pause
				if (player.CurrentTrack == null)
				{
					return;
				}

				if (player.IsPaused)
				{
					await player.ResumeAsync();
				}
				else
				{
					await player.PauseAsync();
				}
				await player.UpdateEmbedAsync(args.Channel);
				break;

			case "3":  // Loop
				player.RepeatMode = player.RepeatMode == TrackRepeatMode.None
					? TrackRepeatMode.Track
					: TrackRepeatMode.None;
				await player.UpdateEmbedAsync(args.Channel);
				break;

			case "4":  // Join
				// Bot joins the user's voice channel via GetPlayerAsync in OnButtonReactionAsync
				await player.UpdateEmbedAsync(args.Channel);
				break;
		}
	}

	/// <summary>
	/// Fetches lyrics for the currently playing track.
	/// </summary>
	public static async Task<string> GetLyricsAsync(CustomQueuedPlayer player)
	{
		if (player.CurrentTrack == null)
		{
			return "No track is currently playing.";
		}

		var title = player.CurrentTrack.Title;
		var artist = player.CurrentTrack.Author;

		// Clean up title - remove common suffixes like "(Official Video)", "[HD]", etc.
		title = CleanTrackTitle(title);
		artist = CleanArtistName(artist);

		try
		{
			// Try lyrics.ovh API first
			var lyrics = await FetchFromLyricsOvhAsync(artist, title);
			if (!string.IsNullOrEmpty(lyrics))
			{
				return FormatLyrics(title, artist, lyrics);
			}

			return $"Could not find lyrics for **{title}** by **{artist}**";
		}
		catch (Exception)
		{
			return $"Error fetching lyrics for **{title}** by **{artist}**";
		}
	}

	private static async Task<string?> FetchFromLyricsOvhAsync(string artist, string title)
	{
		var url = $"https://api.lyrics.ovh/v1/{Uri.EscapeDataString(artist)}/{Uri.EscapeDataString(title)}";

		try
		{
			var response = await _httpClient.GetAsync(url);
			if (!response.IsSuccessStatusCode)
			{
				return null;
			}

			var json = await response.Content.ReadAsStringAsync();
			using var doc = JsonDocument.Parse(json);

			if (doc.RootElement.TryGetProperty("lyrics", out var lyricsElement))
			{
				return lyricsElement.GetString();
			}
		}
		catch
		{
			// Silently fail and return null
		}

		return null;
	}

	private static string CleanTrackTitle(string title)
	{
		// Remove common video suffixes
		var patterns = new[]
		{
			@"\s*\(Official\s*(Music\s*)?Video\)",
			@"\s*\[Official\s*(Music\s*)?Video\]",
			@"\s*\(Official\s*Audio\)",
			@"\s*\[Official\s*Audio\]",
			@"\s*\(Lyrics?\)",
			@"\s*\[Lyrics?\]",
			@"\s*\(HD\)",
			@"\s*\[HD\]",
			@"\s*\(HQ\)",
			@"\s*\[HQ\]",
			@"\s*\(4K\)",
			@"\s*\[4K\]",
			@"\s*\(Visualizer\)",
			@"\s*\[Visualizer\]",
			@"\s*\|.*$",  // Remove everything after |
			@"\s*ft\.?\s*.*$",  // Remove featuring info at end
			@"\s*feat\.?\s*.*$",
		};

		foreach (var pattern in patterns)
		{
			title = Regex.Replace(title, pattern, "", RegexOptions.IgnoreCase);
		}

		return title.Trim();
	}

	private static string CleanArtistName(string artist)
	{
		// Remove " - Topic" suffix from YouTube auto-generated channels
		artist = Regex.Replace(artist, @"\s*-\s*Topic$", "", RegexOptions.IgnoreCase);

		// Remove "VEVO" suffix
		artist = Regex.Replace(artist, @"VEVO$", "", RegexOptions.IgnoreCase);

		return artist.Trim();
	}

	private static string FormatLyrics(string title, string artist, string lyrics)
	{
		const int MaxLength = 1900; // Discord message limit is 2000, leave room for header

		var header = $"**{title}** by **{artist}**\n\n";
		var content = lyrics.Trim();

		if (header.Length + content.Length > MaxLength)
		{
			content = content[..(MaxLength - header.Length - 20)] + "\n\n*[Lyrics truncated]*";
		}

		return header + content;
	}
}

