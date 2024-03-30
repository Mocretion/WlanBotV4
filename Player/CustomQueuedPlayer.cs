using DSharpPlus;
using DSharpPlus.Entities;
using Lavalink4NET.Extensions;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Payloads.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public sealed class CustomQueuedPlayer : QueuedLavalinkPlayer
{
	private JSONManager _jsonManager;
	private DiscordClient _discordClient;

	public CustomQueuedPlayer(IPlayerProperties<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions> properties, DiscordClient discordClient, JSONManager jsonManager) : base(properties)
	{
		_jsonManager = jsonManager;
		_discordClient = discordClient;
	}

	protected override async ValueTask NotifyTrackEndedAsync(ITrackQueueItem queueItem, TrackEndReason endReason, CancellationToken cancellationToken = default)
	{

		if(Queue.Count == 0)
		{
			await base.NotifyTrackEndedAsync(queueItem, endReason, cancellationToken);
			var information = _jsonManager.ServerExists(this.GuildId.ToString());
			var textChannel = await _discordClient.GetChannelAsync(ulong.Parse(information.MusicChannelId));
			UpdateEmbed(textChannel);
		}
		else
		{
			await base.NotifyTrackEndedAsync(queueItem, endReason, cancellationToken);
		}
	}

	protected override async ValueTask NotifyTrackStartedAsync(ITrackQueueItem track, CancellationToken cancellationToken = default)
	{

		await base.NotifyTrackStartedAsync(track, cancellationToken);
		var information = _jsonManager.ServerExists(this.GuildId.ToString());
		var textChannel = await _discordClient.GetChannelAsync(ulong.Parse(information.MusicChannelId));
		UpdateEmbed(textChannel);
	}

	protected override async ValueTask NotifyTrackEnqueuedAsync(ITrackQueueItem queueItem, int position, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ArgumentNullException.ThrowIfNull(queueItem);

		var information = _jsonManager.ServerExists(this.GuildId.ToString());
		var textChannel = await _discordClient.GetChannelAsync(ulong.Parse(information.MusicChannelId));
		UpdateEmbed(textChannel);
	}

	public async void UpdateEmbed(DiscordChannel channel)
	{
		try
		{
			DiscordEmbed embed = EmbedHelper.GenerateEmbed(this);

			DiscordMessage msg = await GetMusicMessage(this.GuildId, channel);
			await msg.ModifyAsync(embed);
		}catch(Exception e)
		{
			Console.WriteLine(e.ToString());
		}
	}

	private async Task<DiscordMessage> GetMusicMessage(ulong guildId, DiscordChannel channel)
	{
		foreach (ServerInformation server in _jsonManager.Servers)
		{
			if (guildId.ToString() == server.Id)
			{
				DiscordMessage msg = await channel.GetMessageAsync(ulong.Parse(server.MusicMessageId));
				return msg;
			}
		}

		return null;
	}
}

