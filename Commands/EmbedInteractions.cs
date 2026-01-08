using DSharpPlus.EventArgs;
using Lavalink4NET.Players.Queued;

public static class EmbedInteractions
{
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
}

