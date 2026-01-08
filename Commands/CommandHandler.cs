using DSharpPlus.Entities;

public static class CommandHandler
{
	public static async Task HandleCommandAsync(string mainCommand, string parameters, CustomQueuedPlayer player, DiscordChannel channel, JSONManager jsonManager)
	{
		switch (mainCommand)
		{
			case "rm":
			case "remove":
				await HandleRemoveAsync(parameters, player, channel);
				break;

			case "tts":
				await HandleTtsAsync(parameters, player, jsonManager);
				break;
		}
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

	private static async Task HandleTtsAsync(string parameters, CustomQueuedPlayer player, JSONManager jsonManager)
	{
		var value = parameters.Trim().ToLower();
		var guildInfo = jsonManager.GetServer(player.GuildId);

		if (guildInfo == null)
		{
			return;
		}

		if (value is "true" or "enable")
		{
			guildInfo.TTSEnabled = true;
			await jsonManager.SaveAsync();
		}
		else if (value is "false" or "disable")
		{
			guildInfo.TTSEnabled = false;
			await jsonManager.SaveAsync();
		}
	}
}
