using DSharpPlus;
using DSharpPlus.Entities;
using Lavalink4NET.Extensions;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Payloads.Events;
using Microsoft.Extensions.Logging;


public sealed class CustomQueuedPlayer : QueuedLavalinkPlayer
{
	private readonly JSONManager _jsonManager;
	private readonly DiscordClient _discordClient;
	private readonly ILogger<CustomQueuedPlayer> _logger;

	/// <summary>
	/// When true, embed updates are suppressed. Used during bulk operations like playlist loading.
	/// </summary>
	public bool SuppressEmbedUpdates { get; set; }

	/// <summary>
	/// The ID of the current lyrics message, if any. Used to delete it when the track changes.
	/// </summary>
	public ulong? LyricsMessageId { get; set; }

	public CustomQueuedPlayer(IPlayerProperties<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions> properties, DiscordClient discordClient, JSONManager jsonManager, ILogger<CustomQueuedPlayer> logger) : base(properties)
	{
		_jsonManager = jsonManager;
		_discordClient = discordClient;
		_logger = logger;
	}

	protected override async ValueTask NotifyTrackEndedAsync(ITrackQueueItem queueItem, TrackEndReason endReason, CancellationToken cancellationToken = default)
	{
		try
		{
			await base.NotifyTrackEndedAsync(queueItem, endReason, cancellationToken);

			if (Queue.Count == 0)
			{
				var information = _jsonManager.GetServer(GuildId);
				if (information == null) return;

				var textChannel = await _discordClient.GetChannelAsync(information.MusicChannelId);
				await UpdateEmbedAsync(textChannel);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error in NotifyTrackEndedAsync for guild {GuildId}", GuildId);
		}
	}

	protected override async ValueTask NotifyTrackStartedAsync(ITrackQueueItem track, CancellationToken cancellationToken = default)
	{
		try
		{
			await base.NotifyTrackStartedAsync(track, cancellationToken);

			var information = _jsonManager.GetServer(GuildId);
			if (information == null) return;

			var textChannel = await _discordClient.GetChannelAsync(information.MusicChannelId);

			// Delete old lyrics message if it exists
			await DeleteLyricsMessageAsync(textChannel);

			await UpdateEmbedAsync(textChannel);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error in NotifyTrackStartedAsync for guild {GuildId}", GuildId);
		}
	}

	protected override async ValueTask NotifyTrackEnqueuedAsync(ITrackQueueItem queueItem, int position, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			ArgumentNullException.ThrowIfNull(queueItem);

			// Skip embed updates during bulk operations (e.g., playlist loading)
			if (SuppressEmbedUpdates) return;

			var information = _jsonManager.GetServer(GuildId);
			if (information == null) return;

			var textChannel = await _discordClient.GetChannelAsync(information.MusicChannelId);
			await UpdateEmbedAsync(textChannel);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error in NotifyTrackEnqueuedAsync for guild {GuildId}", GuildId);
		}
	}

	public async Task UpdateEmbedAsync(DiscordChannel channel)
	{
		try
		{
			var serverInfo = _jsonManager.GetServer(GuildId);
			bool nightcoreEnabled = serverInfo?.NightcoreEnabled ?? false;

			DiscordEmbed embed = EmbedHelper.GenerateEmbed(this, nightcoreEnabled);
			DiscordMessage? msg = await GetMusicMessageAsync(GuildId, channel);

			if (msg != null)
			{
				await msg.ModifyAsync(embed);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to update embed for guild {GuildId}", GuildId);
		}
	}

	private async Task<DiscordMessage?> GetMusicMessageAsync(ulong guildId, DiscordChannel channel)
	{
		try
		{
			var server = _jsonManager.GetServer(guildId);
			if (server == null) return null;

			return await channel.GetMessageAsync(server.MusicMessageId);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Could not get music message for guild {GuildId}", guildId);
			return null;
		}
	}

	/// <summary>
	/// Deletes the current lyrics message if one exists.
	/// </summary>
	public async Task DeleteLyricsMessageAsync(DiscordChannel? channel = null)
	{
		if (LyricsMessageId == null) return;

		try
		{
			if (channel == null)
			{
				var information = _jsonManager.GetServer(GuildId);
				if (information == null) return;
				channel = await _discordClient.GetChannelAsync(information.MusicChannelId);
			}

			var lyricsMessage = await channel.GetMessageAsync(LyricsMessageId.Value);
			await lyricsMessage.DeleteAsync();
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Could not delete lyrics message for guild {GuildId}", GuildId);
		}
		finally
		{
			LyricsMessageId = null;
		}
	}
}

