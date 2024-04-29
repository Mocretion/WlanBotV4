using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Lavalink4NET.Extensions;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Players;
using Lavalink4NET;
using Microsoft.Extensions.Logging;
using Lavalink4NET.Rest.Entities.Tracks;
using Microsoft.Extensions.Options;
using Lavalink4NET.Rest;
using Lavalink4NET.Tracks;
using System.Numerics;

var builder = new HostApplicationBuilder(args);

// DSharpPlus
builder.Services.AddHostedService<ApplicationHost>();
builder.Services.AddSingleton<DiscordClient>();
builder.Services.AddSingleton(
    new DiscordConfiguration
    {
        Token = "",
        Intents = DiscordIntents.All
    }
    ); // Put token here

// Lavalink
builder.Services.AddLavalink();
builder.Services.ConfigureLavalink(config =>
{
    config.BaseAddress = new Uri("http://localhost:2333");
    config.ReadyTimeout = TimeSpan.FromSeconds(10);
    config.Passphrase = "youshallnotpass";
});


// Logging
builder.Services.AddLogging(s => s.AddConsole().SetMinimumLevel(LogLevel.Debug));

builder.Build().Run();

file sealed class ApplicationHost : BackgroundService
{

    private readonly DiscordClient _discordClient;
    private readonly IAudioService _audioService;
    private readonly JSONManager _jsonManager;

    public ApplicationHost(DiscordClient discordClient, IAudioService audioService)
    {
        ArgumentNullException.ThrowIfNull(discordClient);
        ArgumentNullException.ThrowIfNull(audioService);

        _discordClient = discordClient;
        _audioService = audioService;
        _jsonManager = new JSONManager();
        var slash = _discordClient.UseSlashCommands();
        slash.RegisterCommands<SlashCommands>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // connect to discord gateway and initialize node connection
        await _discordClient
            .ConnectAsync()
            .ConfigureAwait(false);

        var readyTaskCompletionSource = new TaskCompletionSource();

        Task SetResult(DiscordClient client, ReadyEventArgs eventArgs)
        {
            readyTaskCompletionSource.TrySetResult();
            return Task.CompletedTask;
        }

        await _jsonManager.ReadJSON();

        _discordClient.Ready += SetResult;
        _discordClient.Ready += OnClientReady;
        _discordClient.MessageCreated += OnMessageCreated;  // User sends messahge in some channel
        _discordClient.ComponentInteractionCreated += OnButtonReactionAsync;  // User clicks button
        _discordClient.MessageDeleted += OnMessageDeletedAsync;  // When user deletes message

        await readyTaskCompletionSource.Task.ConfigureAwait(false);

        //_discordClient.MessageDeleted -= OnMessageDeletedAsync;  Where tf put these?
        //_discordClient.ComponentInteractionCreated -= OnButtonReactionAsync;
        //_discordClient.MessageCreated -= OnMessageCreated;
        _discordClient.Ready -= OnClientReady;
        _discordClient.Ready -= SetResult;
    }

    private async Task<Task> OnClientReady(DiscordClient sender, ReadyEventArgs e)
    {

        foreach (ServerInformation server in _jsonManager.Servers)
        {

            IReadOnlyList<DiscordChannel> botChannels = await _discordClient.Guilds[ulong.Parse(server.Id)].GetChannelsAsync(); // Get all channels in guild

            DiscordChannel botChannel = botChannels.First(x => x.Id == ulong.Parse(server.MusicChannelId));  // Get the channel the bot is using

            // Delete old message, new message gets created in MessageDeleted event
            try
            {
                DiscordMessage oldMessage = await botChannel.GetMessageAsync(ulong.Parse(server.MusicMessageId));
                await oldMessage.DeleteAsync();
            }
            catch (Exception)
            {
                await CreateMusicMessage(server);
            }
        }

        return Task.CompletedTask;
    }

    private async Task OnMessageDeletedAsync(DiscordClient sender, MessageDeleteEventArgs args)
    {
        ServerInformation guildInfo = _jsonManager.ServerExists(args.Guild.Id.ToString());

        if (guildInfo.MusicMessageId != args.Message.Id.ToString()) return;

        await CreateMusicMessage(guildInfo);
    }

    public async Task CreateMusicMessage(ServerInformation guildInfo)
    {
        var builder = new DiscordMessageBuilder();  // Generate DJ Message
        builder.WithEmbed(EmbedHelper.GenerateEmbed())
            .AddComponents(EmbedHelper.GenerateButtonComponents());

        IReadOnlyList<DiscordChannel> botChannels = await _discordClient.Guilds[ulong.Parse(guildInfo.Id)].GetChannelsAsync(); // Get all channels in guild

        DiscordChannel botChannel = botChannels.First(x => x.Id == ulong.Parse(guildInfo.MusicChannelId));  // Get the channel the bot is using

        DiscordMessage newMessage = await botChannel.SendMessageAsync(builder);  // Send new message

        guildInfo.MusicMessageId = newMessage.Id.ToString();  // Update Server Info with new message
        await _jsonManager.UpdateServerList();

        // Delete up to 10 messages before embed to clean up
        IReadOnlyList<DiscordMessage> messages = await botChannel.GetMessagesBeforeAsync(newMessage.Id, 10);

        if (messages != null && messages.Count > 0)
            await botChannel.DeleteMessagesAsync(messages);
    }

    /// <summary>
    /// Fired when a user posts a message.
    /// Use PINEAPPLE to init the bot for a guild.
    /// Only takes messages in the PINAPPLE chat into account.
    /// </summary>
    /// <param name="senderClient"> User who sent the message </param>
    /// <returns></returns>
    private async Task OnMessageCreated(DiscordClient senderClient, MessageCreateEventArgs args)
    {
        DiscordMember sender = args.Message.Author as DiscordMember;

        if (sender == null) return;

        ServerInformation guildInfo = _jsonManager.ServerExists(sender.Guild.Id.ToString());

        if (CheckIfMessageIsInit(args, guildInfo))  // Check if user wants to init the bot and if so add this server to the list
        {
            if (guildInfo == null)
                guildInfo = _jsonManager.ServerExists(sender.Guild.Id.ToString());

            var builder = new DiscordMessageBuilder();
            builder.WithEmbed(EmbedHelper.GenerateEmbed())
                .AddComponents(EmbedHelper.GenerateButtonComponents());

            DiscordMessage message = await args.Channel.SendMessageAsync(builder);
            guildInfo.MusicMessageId = message.Id.ToString();
            await _jsonManager.UpdateServerList();
            await args.Message.DeleteAsync();
            return;
        }

        if (!CheckIfMessageIsInCorrectChannel(args))
        {
            return;
        }

        if (args.Author.IsBot)
        {
            if (!args.Author.IsCurrent)
                await args.Message.DeleteAsync();

            return;
        }

        bool isCommand = await CheckIsCommand(sender, sender.Guild.Id, args);

        if (isCommand)  // Check if message is a command and execute it.
        {
            await args.Message.DeleteAsync();
            return;
        }

        if (sender.VoiceState == null || sender.VoiceState.Channel.Type != ChannelType.Voice)  // Sender not in channel
        {
            await sender.SendMessageAsync("Error 🤡: Du dich hierfür in einem Voice Channel befinden");
            await args.Message.DeleteAsync();
            return;
        }

        var player = await GetPlayerAsync(sender.Guild.Id, sender.VoiceState.Channel.Id, true).ConfigureAwait(false);
        if (player == null)  // Check if there is no Lavalink connection
        {
            await sender.SendMessageAsync("Error 1: Verbindung fehlgeschlagen");
            await args.Message.DeleteAsync();
            return;
        }

        // Search for input
        LavalinkTrack track = await _audioService.Tracks.LoadTrackAsync(args.Message.Content, TrackSearchMode.YouTube).ConfigureAwait(false);

        if (track == null)  // No matches for input
        {
            await sender.SendMessageAsync("Error 2: Video konnte nicht gefunden werden");
            await args.Message.DeleteAsync();
            return;
        }

        if (track.Uri.ToString() == "https://www.youtube.com/watch?v=mQfkFxUQKD8")
        {
            await sender.SendMessageAsync("Error 3: Du kleiner goh");
            await args.Message.DeleteAsync();
            return;
        }

        if (guildInfo.TTSEnabled)  // Don't have TTS Plugin yet
        {
            LavalinkTrack ttsTrack = await _audioService.Tracks.GetTextToSpeechTrackAsync("Now playing." + track.Title);

            if (ttsTrack != null)
                await player.PlayAsync(ttsTrack).ConfigureAwait(false);
        }


        await player.PlayAsync(track).ConfigureAwait(false);  // Queues the track

        player.UpdateEmbed(args.Channel);

        await args.Message.DeleteAsync();
    }

    /// <summary>
    /// Check if the message is the init codeword "PINEAPPLE". If it is add this server to the bots serverlist
    /// </summary>
    /// <param name="args"></param>
    /// <param name="server"></param>
    /// <returns></returns>
    private bool CheckIfMessageIsInit(MessageCreateEventArgs args, ServerInformation server)
    {
        if (args.Message.Content == "PINEAPPLE")  // Special word to init the bot
        {
            if (server == null)
                _jsonManager.Servers.Add(new ServerInformation
                {
                    Id = args.Guild.Id.ToString(),
                    MusicChannelId = args.Channel.Id.ToString(),
                });
            else
                server.MusicChannelId = args.Channel.Id.ToString();

            return true;
        }

        return false;
    }

    private bool CheckIfMessageIsInCorrectChannel(MessageCreateEventArgs args)
    {
        foreach (ServerInformation server in _jsonManager.Servers)
        {
            if (args.Channel.Id.ToString() == server.MusicChannelId)
                return true;
        }

        return false;
    }

    private async Task<bool> CheckIsCommand(DiscordMember sender, ulong guildId, MessageCreateEventArgs args)
    {

        if (args.Message.Content.StartsWith("!"))
        {

            if (args.Message.Content.Length == 1)
                return true;

            await HandleCommandAsync(sender, guildId, args.Channel, args.Message.Content.Substring(1));
            return true;
        }

        return false;
    }

    private async Task HandleCommandAsync(DiscordMember sender, ulong guildId, DiscordChannel channel, string command)
    {
        string[] cmd = command.ToLower().Split(separator: new char[] { ' ' }, count: 2);

        if (cmd.Length != 2)
        {
            cmd = new string[] { cmd[0], "" };
        }

        string mainCommand = cmd[0];
        string parameters = cmd[1];

        var player = await GetPlayerAsync(guildId, sender.VoiceState.Channel.Id, false).ConfigureAwait(false);

        if (player == null)
            return;

        await CommandHandler.HandleCommandAsync(mainCommand, parameters, player, channel, _jsonManager);
    }

    private async Task OnButtonReactionAsync(DiscordClient clientSender, ComponentInteractionCreateEventArgs args)
    {
        await args.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

        string buttonId = args.Id;

        DiscordMember sender = args.User as DiscordMember;

        if (sender.VoiceState.Channel == null) return;

        var player = await GetPlayerAsync(args.Guild.Id, sender.VoiceState.Channel.Id, true).ConfigureAwait(false);
        if (player == null)
            return;

        await EmbedInteractions.HandleButtonAsync(buttonId, player, args);
    }

    private async ValueTask<CustomQueuedPlayer?> GetPlayerAsync(ulong guildId, ulong memberVoiceChannel, bool connectToVoiceChannel = true)
    {
        // Only allow the player to connect if connectToVoiceChannel is true
        var channelBehavior = connectToVoiceChannel
            ? PlayerChannelBehavior.Join
            : PlayerChannelBehavior.None;

        var retrieveOptions = new PlayerRetrieveOptions(ChannelBehavior: channelBehavior);

        // Get the guilds player or create a Queues
        var playerOptions = Options.Create(new QueuedLavalinkPlayerOptions
        {
            SelfDeaf = true
        });

        var playerFactory = PlayerFactory.Create<CustomQueuedPlayer, QueuedLavalinkPlayerOptions>(properties => new CustomQueuedPlayer(properties, _discordClient, _jsonManager));

        var result = await _audioService.Players
            .RetrieveAsync(guildId, memberVoiceChannel, playerFactory, playerOptions, retrieveOptions)
            .ConfigureAwait(false);

        // Error handling
        if (!result.IsSuccess)
        {
            var errorMessage = result.Status switch
            {
                PlayerRetrieveStatus.UserNotInVoiceChannel => "You are not connected to a voice channel.",
                PlayerRetrieveStatus.BotNotConnected => "The bot is currently not connected.",
                _ => "Unknown error.",
            };

            // send errorMessage to user
            return null;
        }

        return result.Player;
    }
}
