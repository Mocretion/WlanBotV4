using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

public class SlashCommands : ApplicationCommandModule
{
    [ContextMenu(ApplicationCommandType.UserContextMenu, "Molest")]
    public async Task UserMenu(ContextMenuContext ctx)
    {
        DiscordMember? sender = ctx.User as DiscordMember;
        DiscordMember? sended = ctx.TargetUser as DiscordMember;

        if (sender == null || sended == null)
        {
            var response = new DiscordInteractionResponseBuilder().WithContent("Could not molest :c !");
            response.IsEphemeral = true;

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, response);
            return;
        }

        try
        {
            await sended.SendMessageAsync($"You just got molested by {sender.DisplayName}");

            var response = new DiscordInteractionResponseBuilder().WithContent("Molested!");
            response.IsEphemeral = true;

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, response);
        }
        catch (Exception ex)
        {
            var response = new DiscordInteractionResponseBuilder().WithContent("Could not molest :c !");
            response.IsEphemeral = true;

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, response);
        }

    }
}
