using DSharpPlus.EventArgs;
using Lavalink4NET.Players.Queued;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;


public class EmbedInteractions
{
	public static async Task HandleButtonAsync(string buttonId, CustomQueuedPlayer player, ComponentInteractionCreateEventArgs args)
	{
		switch (buttonId)
		{
			case "0":  // Stop
				await player.Queue.ClearAsync();
				player.RepeatMode = TrackRepeatMode.None;
				await player.StopAsync();
				player.UpdateEmbed(args.Channel);
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
				player.UpdateEmbed(args.Channel);
				break;
			case "2":  // Pause

				if (player.CurrentTrack == null)
					return;

				if (player.IsPaused)
				{
					await player.ResumeAsync();
				}
				else
				{
					await player.PauseAsync();
				}
				player.UpdateEmbed(args.Channel);
				break;
			case "3":  // Loop
				if (player.RepeatMode == TrackRepeatMode.None)
				{
					player.RepeatMode = TrackRepeatMode.Track;
				}
				else
				{
					player.RepeatMode = TrackRepeatMode.None;
				}
				player.UpdateEmbed(args.Channel);
				break;
		}
	}

}

