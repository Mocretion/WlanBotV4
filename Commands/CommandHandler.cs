using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;


public class CommandHandler
{
	/// <summary>
	/// Executes a command (messages stzaring with !)
	/// </summary>
	/// <param name="mainCommand"> The main command like 'remove' </param>
	/// <param name="other"> Everything after the main command </param>
	/// <param name="player"> The LavalinkPlayer </param>
	/// <param name="channel"> The channel the command was posted in </param>
	/// <returns></returns>
	public static async Task HandleCommandAsync(string mainCommand, string other, CustomQueuedPlayer player, DiscordChannel channel, JSONManager jsonManager)
	{
		switch (mainCommand)
		{
			case "rm":
			case "remove":
				other = other.Replace(" ", "");  // Replace user mistake of adding spaces
				string[] ids = other.Split(',');  // Get every id to remove

				bool changed = false;

				foreach (string id in ids)
				{
					if (int.TryParse(id, out int idInt) && idInt >= 0)
					{
						await player.Queue.RemoveAtAsync(idInt);
						changed = true;
					}
				}

				if (changed)
					player.UpdateEmbed(channel);

				break;
			case "tts":
				other = other.Trim();
				ServerInformation guildInfo;

				if (other == "true" || other == "enable")
				{
					guildInfo = jsonManager.ServerExists(player.GuildId.ToString());
					guildInfo.TTSEnabled = true;
					await jsonManager.UpdateServerList();

				}
				else if(other == "false" || other == "disable")
				{
					guildInfo = jsonManager.ServerExists(player.GuildId.ToString());
					guildInfo.TTSEnabled = false;
					await jsonManager.UpdateServerList();
				}
				break;
		}
	}
}
