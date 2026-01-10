using DSharpPlus.Entities;

public static class CommandHandler
{
	// Commands that require a player
	public static async Task HandleCommandAsync(string mainCommand, string parameters, CustomQueuedPlayer player, DiscordChannel channel, JSONManager jsonManager, DiscordMember sender)
	{
		switch (mainCommand)
		{
			case "rm":
			case "remove":
				await HandleRemoveAsync(parameters, player, channel);
				break;

			case "shuffle":
				await HandleShuffleAsync(player, channel);
				break;

			case "nightcore":
			case "nc":
				await HandleNightcoreAsync(player, channel, jsonManager);
				break;

			case "help":
				await HandleHelpAsync(sender);
				break;

			case "lyrics":
			case "lyric":
				await HandleLyricsAsync(player, channel);
				break;
		}
	}

	// Commands that don't require a player
	public static async Task HandleCommandWithoutPlayerAsync(string mainCommand, ulong guildId, DiscordChannel channel, JSONManager jsonManager, DiscordMember sender)
	{
		switch (mainCommand)
		{
			case "help":
				await HandleHelpAsync(sender);
				break;

			case "nightcore":
			case "nc":
				await HandleNightcoreWithoutPlayerAsync(guildId, channel, jsonManager);
				break;
		}
	}

	public static bool RequiresPlayer(string mainCommand)
	{
		return mainCommand switch
		{
			"help" => false,
			"nightcore" => false,
			"nc" => false,
			_ => true
		};
	}

	private static async Task HandleRemoveAsync(string parameters, CustomQueuedPlayer player, DiscordChannel channel)
	{
		var input = parameters.Replace(" ", "");
		var ids = input.Split(',');

		// Parse and validate all indices first, sort descending to avoid index shifting
		var validIndices = ids
			.Select(id => int.TryParse(id, out int idx) ? idx : -1)
			.Where(idx => idx >= 0 && idx < player.Queue.Count)
			.OrderByDescending(idx => idx)
			.Distinct()
			.ToList();

		if (validIndices.Count == 0)
		{
			return;
		}

		foreach (var index in validIndices)
		{
			await player.Queue.RemoveAtAsync(index);
		}

		await player.UpdateEmbedAsync(channel);
	}

	private static async Task HandleShuffleAsync(CustomQueuedPlayer player, DiscordChannel channel)
	{
		if (player.Queue.Count < 2)
		{
			return;
		}

		await player.Queue.ShuffleAsync();
		await player.UpdateEmbedAsync(channel);
	}

	private static async Task HandleLyricsAsync(CustomQueuedPlayer player, DiscordChannel channel)
	{
		// Delete previous lyrics message if exists
		await player.DeleteLyricsMessageAsync(channel);

		var lyrics = await EmbedInteractions.GetLyricsAsync(player);
		var message = await channel.SendMessageAsync(lyrics);

		// Store the message ID so it can be deleted when track changes
		player.LyricsMessageId = message.Id;
	}

	private static async Task HandleHelpAsync(DiscordMember sender)
	{
		var helpMessage = """
			**Sus Music Bot Commands**

			**Playback**
			`!shuffle` - Shuffle the queue
			`!rm <indices>` / `!remove <indices>` - Remove tracks from queue (e.g., `!rm 0,2,5`)
			`!lyrics` / `!lyric` - Show lyrics for the current track

			**Modes**
			`!nightcore` / `!nc` - Toggle nightcore mode

			**Buttons**
			`Stop` - Stop playback and clear queue
			`Skip` - Skip to next track
			`Pause` - Pause/Resume playback
			`Loop` - Toggle loop mode for current track
			`Join` - Make bot join your voice channel
			""";

		try
		{
			await sender.SendMessageAsync(helpMessage);
		}
		catch
		{
			// User may have DMs disabled
		}
	}

	private static async Task HandleNightcoreAsync(CustomQueuedPlayer player, DiscordChannel channel, JSONManager jsonManager)
	{
		var guildInfo = jsonManager.GetServer(player.GuildId);
		if (guildInfo == null)
		{
			return;
		}

		// Toggle nightcore
		guildInfo.NightcoreEnabled = !guildInfo.NightcoreEnabled;
		await jsonManager.SaveAsync();

		// Apply or remove audio filters
		if (guildInfo.NightcoreEnabled)
		{
			player.Filters.Timescale = new Lavalink4NET.Filters.TimescaleFilterOptions(
				Speed: 1.25f,
				Pitch: 1.25f,
				Rate: 1.0f
			);
		}
		else
		{
			// Clear timescale filter
			player.Filters.Timescale = null;
		}

		await player.Filters.CommitAsync();
		await player.UpdateEmbedAsync(channel);
	}

	private static async Task HandleNightcoreWithoutPlayerAsync(ulong guildId, DiscordChannel channel, JSONManager jsonManager)
	{
		var guildInfo = jsonManager.GetServer(guildId);
		if (guildInfo == null)
		{
			return;
		}

		// Toggle nightcore setting (filters will be applied when player connects)
		guildInfo.NightcoreEnabled = !guildInfo.NightcoreEnabled;
		await jsonManager.SaveAsync();

		// Update embed to reflect the change
		var musicMessageId = guildInfo.MusicMessageId;
		try
		{
			var message = await channel.GetMessageAsync(musicMessageId);
			var embed = EmbedHelper.GenerateEmbedIdle(guildInfo.NightcoreEnabled);
			await message.ModifyAsync(embed);
		}
		catch
		{
			// Message might not exist
		}
	}
}
