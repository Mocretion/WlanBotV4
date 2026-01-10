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
using DSharpPlus.VoiceNext;

// Load .env file if it exists
var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
if (File.Exists(envPath))
{
    foreach (var line in File.ReadAllLines(envPath))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;

        var separatorIndex = trimmed.IndexOf('=');
        if (separatorIndex <= 0) continue;

        var key = trimmed[..separatorIndex].Trim();
        var value = trimmed[(separatorIndex + 1)..].Trim();
        Environment.SetEnvironmentVariable(key, value);
    }
}

var builder = new HostApplicationBuilder(args);

// Logging (register early so other services can use it)
builder.Services.AddLogging(s => s.AddConsole().SetMinimumLevel(LogLevel.Debug));

// Configuration
builder.Services.AddSingleton<JSONManager>();

// DSharpPlus
builder.Services.AddHostedService<ApplicationHost>();
builder.Services.AddSingleton<DiscordClient>();
builder.Services.AddSingleton(
    new DiscordConfiguration
    {
        Token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN")
            ?? throw new InvalidOperationException("DISCORD_BOT_TOKEN environment variable not set"),
        Intents = DiscordIntents.All
    });

// Lavalink
builder.Services.AddLavalink();
builder.Services.ConfigureLavalink(config =>
{
    config.BaseAddress = new Uri(Environment.GetEnvironmentVariable("LAVALINK_URL") ?? "http://localhost:2333");
    config.ReadyTimeout = TimeSpan.FromSeconds(10);
    config.Passphrase = Environment.GetEnvironmentVariable("LAVALINK_PASSWORD") ?? "youshallnotpass";
});

builder.Build().Run();

file sealed class ApplicationHost : BackgroundService
{
    private const int MaxPlaylistTracks = 100;

    private readonly DiscordClient _discordClient;
    private readonly IAudioService _audioService;
    private readonly JSONManager _jsonManager;
    private readonly ILogger<ApplicationHost> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public ApplicationHost(DiscordClient discordClient, IAudioService audioService, JSONManager jsonManager, ILogger<ApplicationHost> logger, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(discordClient);
        ArgumentNullException.ThrowIfNull(audioService);
        ArgumentNullException.ThrowIfNull(jsonManager);

        _discordClient = discordClient;
        _audioService = audioService;
        _jsonManager = jsonManager;
        _logger = logger;
        _loggerFactory = loggerFactory;

        var slash = _discordClient.UseSlashCommands();
        slash.RegisterCommands<SlashCommands>();

        // Initialize VoiceNext for voice channel connections
        _discordClient.UseVoiceNext();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _jsonManager.LoadAsync();

        _discordClient.Ready += OnClientReadyAsync;
        _discordClient.MessageCreated += OnMessageCreatedAsync;
        _discordClient.ComponentInteractionCreated += OnButtonReactionAsync;
        _discordClient.MessageDeleted += OnMessageDeletedAsync;

        await _discordClient.ConnectAsync();

        _logger.LogInformation("Bot connected to Discord");

        // Keep the service running until cancellation is requested
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Shutdown requested");
        }
        finally
        {
            _discordClient.Ready -= OnClientReadyAsync;
            _discordClient.MessageCreated -= OnMessageCreatedAsync;
            _discordClient.ComponentInteractionCreated -= OnButtonReactionAsync;
            _discordClient.MessageDeleted -= OnMessageDeletedAsync;

            await _discordClient.DisconnectAsync();
        }
    }

    private async Task OnClientReadyAsync(DiscordClient sender, ReadyEventArgs e)
    {
        try
        {
            _logger.LogInformation("Discord client ready, initializing {Count} servers", _jsonManager.Servers.Count());

            foreach (ServerInformation server in _jsonManager.Servers)
            {
                try
                {
                    if (!_discordClient.Guilds.TryGetValue(server.Id, out var guild))
                    {
                        _logger.LogWarning("Guild {GuildId} not found", server.Id);
                        continue;
                    }

                    var botChannels = await guild.GetChannelsAsync();
                    var botChannel = botChannels.FirstOrDefault(x => x.Id == server.MusicChannelId);

                    if (botChannel == null)
                    {
                        _logger.LogWarning("Music channel {ChannelId} not found in guild {GuildId}", server.MusicChannelId, server.Id);
                        continue;
                    }

                    try
                    {
                        var oldMessage = await botChannel.GetMessageAsync(server.MusicMessageId);
                        await oldMessage.DeleteAsync();
                    }
                    catch
                    {
                        await CreateMusicMessageAsync(server);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error initializing server {ServerId}", server.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnClientReadyAsync");
        }
    }

    private async Task OnMessageDeletedAsync(DiscordClient sender, MessageDeleteEventArgs args)
    {
        try
        {
            if (args.Guild == null) return;

            var guildInfo = _jsonManager.GetServer(args.Guild.Id);
            if (guildInfo == null) return;

            if (guildInfo.MusicMessageId != args.Message.Id) return;

            await CreateMusicMessageAsync(guildInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnMessageDeletedAsync for guild {GuildId}", args.Guild?.Id);
        }
    }

    private async Task CreateMusicMessageAsync(ServerInformation guildInfo)
    {
        try
        {
            var messageBuilder = new DiscordMessageBuilder()
                .WithEmbed(EmbedHelper.GenerateEmbed())
                .AddComponents(EmbedHelper.GenerateButtonComponents());

            if (!_discordClient.Guilds.TryGetValue(guildInfo.Id, out var guild))
            {
                _logger.LogWarning("Guild {GuildId} not found when creating music message", guildInfo.Id);
                return;
            }

            var botChannels = await guild.GetChannelsAsync();
            var botChannel = botChannels.FirstOrDefault(x => x.Id == guildInfo.MusicChannelId);

            if (botChannel == null)
            {
                _logger.LogWarning("Music channel {ChannelId} not found in guild {GuildId}", guildInfo.MusicChannelId, guildInfo.Id);
                return;
            }

            var newMessage = await botChannel.SendMessageAsync(messageBuilder);

            guildInfo.MusicMessageId = newMessage.Id;
            await _jsonManager.SaveAsync();

            // Delete up to 10 messages before embed to clean up
            var messages = await botChannel.GetMessagesBeforeAsync(newMessage.Id, 10);

            if (messages != null && messages.Count > 0)
            {
                await botChannel.DeleteMessagesAsync(messages);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating music message for guild {GuildId}", guildInfo.Id);
        }
    }

    private async Task OnMessageCreatedAsync(DiscordClient senderClient, MessageCreateEventArgs args)
    {
        try
        {
            if (args.Guild == null) return;

            var sender = args.Message.Author as DiscordMember;
            if (sender == null) return;

            var guildInfo = _jsonManager.GetServer(sender.Guild.Id);

            // Check if user wants to init the bot
            if (await TryHandleInitMessageAsync(args, guildInfo))
            {
                return;
            }

            // Only process messages in configured music channels
            if (!IsMessageInMusicChannel(args))
            {
                return;
            }

            // Delete messages from other bots
            if (args.Author.IsBot)
            {
                if (!args.Author.IsCurrent)
                {
                    await args.Message.DeleteAsync();
                }
                return;
            }

            // Handle commands
            if (await TryHandleCommandAsync(sender, sender.Guild.Id, args))
            {
                await args.Message.DeleteAsync();
                return;
            }

            // Validate voice state
            if (sender.VoiceState?.Channel == null || sender.VoiceState.Channel.Type != ChannelType.Voice)
            {
                await sender.SendMessageAsync("Error: Du musst dich in einem Voice Channel befinden");
                await args.Message.DeleteAsync();
                return;
            }

            var player = await GetPlayerAsync(sender.Guild.Id, sender.VoiceState.Channel.Id, true);
            if (player == null)
            {
                await sender.SendMessageAsync("Error 1: Verbindung fehlgeschlagen");
                await args.Message.DeleteAsync();
                return;
            }

            // Check if it's a playlist URL
            if (IsPlaylistUrl(args.Message.Content))
            {
                var loadResult = await _audioService.Tracks.LoadTracksAsync(args.Message.Content, TrackSearchMode.None);

                if (!loadResult.IsSuccess || loadResult.Playlist is null || !loadResult.Tracks.Any())
                {
                    await sender.SendMessageAsync("Error 2: Playlist konnte nicht geladen werden");
                    await args.Message.DeleteAsync();
                    return;
                }

                int totalTracks = loadResult.Tracks.Count();
                int addedCount = 0;

                // Suppress embed updates during bulk add to avoid rate limiting
                player.SuppressEmbedUpdates = true;
                try
                {
                    // Limit playlist size and enumerate directly without materializing full list
                    foreach (var track in loadResult.Tracks.Take(MaxPlaylistTracks))
                    {
                        if (track.Uri?.ToString() == "https://www.youtube.com/watch?v=mQfkFxUQKD8")
                        {
                            continue;
                        }

                        await player.PlayAsync(track);
                        addedCount++;
                    }
                }
                finally
                {
                    player.SuppressEmbedUpdates = false;
                }

                if (totalTracks > MaxPlaylistTracks)
                {
                    _logger.LogInformation("Added {Count} tracks from playlist '{PlaylistName}' (limited from {Total})", addedCount, loadResult.Playlist.Name, totalTracks);
                }
                else
                {
                    _logger.LogInformation("Added {Count} tracks from playlist '{PlaylistName}'", addedCount, loadResult.Playlist.Name);
                }

                await player.UpdateEmbedAsync(args.Channel);
                await args.Message.DeleteAsync();
                return;
            }

            // Search for single track
            var singleTrack = await _audioService.Tracks.LoadTrackAsync(args.Message.Content, TrackSearchMode.YouTube);

            if (singleTrack == null)
            {
                await sender.SendMessageAsync("Error 2: Video konnte nicht gefunden werden");
                await args.Message.DeleteAsync();
                return;
            }

            if (singleTrack.Uri?.ToString() == "https://www.youtube.com/watch?v=mQfkFxUQKD8")
            {
                await sender.SendMessageAsync("Error 3: Du kleiner goh");
                await args.Message.DeleteAsync();
                return;
            }

            await player.PlayAsync(singleTrack);
            await player.UpdateEmbedAsync(args.Channel);
            await args.Message.DeleteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnMessageCreatedAsync for guild {GuildId}", args.Guild?.Id);
        }
    }

    private async Task<bool> TryHandleInitMessageAsync(MessageCreateEventArgs args, ServerInformation? existingServer)
    {
        if (args.Message.Content != "PINEAPPLE")
        {
            return false;
        }

        var guildInfo = _jsonManager.GetOrCreateServer(args.Guild.Id, args.Channel.Id);

        var messageBuilder = new DiscordMessageBuilder()
            .WithEmbed(EmbedHelper.GenerateEmbed())
            .AddComponents(EmbedHelper.GenerateButtonComponents());

        var message = await args.Channel.SendMessageAsync(messageBuilder);
        guildInfo.MusicMessageId = message.Id;
        await _jsonManager.SaveAsync();
        await args.Message.DeleteAsync();

        _logger.LogInformation("Bot initialized for guild {GuildId} in channel {ChannelId}", args.Guild.Id, args.Channel.Id);

        return true;
    }

    private bool IsMessageInMusicChannel(MessageCreateEventArgs args)
    {
        var server = _jsonManager.GetServer(args.Guild.Id);
        return server != null && args.Channel.Id == server.MusicChannelId;
    }

    private async Task<bool> TryHandleCommandAsync(DiscordMember sender, ulong guildId, MessageCreateEventArgs args)
    {
        if (!args.Message.Content.StartsWith("!"))
        {
            return false;
        }

        if (args.Message.Content.Length == 1)
        {
            return true;
        }

        await HandleCommandAsync(sender, guildId, args.Channel, args.Message.Content[1..]);
        return true;
    }

    private async Task HandleCommandAsync(DiscordMember sender, ulong guildId, DiscordChannel channel, string command)
    {
        string[] cmd = command.ToLower().Split(separator: [' '], count: 2);
        string mainCommand = cmd[0];
        string parameters = cmd.Length > 1 ? cmd[1] : "";

        // Handle commands that don't require a player
        if (!CommandHandler.RequiresPlayer(mainCommand))
        {
            // If user is in voice and player exists, use the player-aware version
            if (sender.VoiceState?.Channel != null)
            {
                var player = await GetPlayerAsync(guildId, sender.VoiceState.Channel.Id, false);
                if (player != null)
                {
                    await CommandHandler.HandleCommandAsync(mainCommand, parameters, player, channel, _jsonManager, sender);
                    return;
                }
            }

            // Otherwise use the playerless version
            await CommandHandler.HandleCommandWithoutPlayerAsync(mainCommand, guildId, channel, _jsonManager, sender);
            return;
        }

        // Commands that require player
        if (sender.VoiceState?.Channel == null)
        {
            return;
        }

        var playerForCommand = await GetPlayerAsync(guildId, sender.VoiceState.Channel.Id, false);
        if (playerForCommand == null)
        {
            return;
        }

        await CommandHandler.HandleCommandAsync(mainCommand, parameters, playerForCommand, channel, _jsonManager, sender);
    }

    private async Task OnButtonReactionAsync(DiscordClient clientSender, ComponentInteractionCreateEventArgs args)
    {
        try
        {
            await args.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

            // Handle Join button separately
            if (args.Id == "4")
            {
                var member = await args.Guild.GetMemberAsync(args.User.Id);
                var voiceChannel = member?.VoiceState?.Channel;
                if (voiceChannel != null)
                {
                    var vnext = clientSender.GetVoiceNext();
                    var existingConnection = vnext.GetConnection(args.Guild);
                    if (existingConnection == null)
                    {
                        await vnext.ConnectAsync(voiceChannel);
                    }
                }
                return;
            }

            var sender = await args.Guild.GetMemberAsync(args.User.Id);
            if (sender?.VoiceState?.Channel == null)
            {
                return;
            }

            var player = await GetPlayerAsync(args.Guild.Id, sender.VoiceState.Channel.Id, true);
            if (player == null)
            {
                return;
            }

            await EmbedInteractions.HandleButtonAsync(args.Id, player, args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnButtonReactionAsync for guild {GuildId}", args.Guild?.Id);
        }
    }

    private static bool IsPlaylistUrl(string input)
    {
        // Check for YouTube playlist URL patterns
        if (string.IsNullOrWhiteSpace(input))
            return false;

        // YouTube playlist URLs contain "list=" parameter
        return input.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) &&
               input.Contains("list=", StringComparison.OrdinalIgnoreCase);
    }

    private async ValueTask<CustomQueuedPlayer?> GetPlayerAsync(ulong guildId, ulong memberVoiceChannel, bool connectToVoiceChannel = true)
    {
        try
        {
            var channelBehavior = connectToVoiceChannel
                ? PlayerChannelBehavior.Join
                : PlayerChannelBehavior.None;

            var retrieveOptions = new PlayerRetrieveOptions(ChannelBehavior: channelBehavior);

            var playerOptions = Options.Create(new QueuedLavalinkPlayerOptions
            {
                SelfDeaf = true
            });

            var playerLogger = _loggerFactory.CreateLogger<CustomQueuedPlayer>();
            var playerFactory = PlayerFactory.Create<CustomQueuedPlayer, QueuedLavalinkPlayerOptions>(
                properties => new CustomQueuedPlayer(properties, _discordClient, _jsonManager, playerLogger));

            var result = await _audioService.Players
                .RetrieveAsync(guildId, memberVoiceChannel, playerFactory, playerOptions, retrieveOptions);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to get player for guild {GuildId}: {Status}", guildId, result.Status);
                return null;
            }

            // Apply nightcore filters if enabled for this server
            var serverInfo = _jsonManager.GetServer(guildId);
            if (serverInfo?.NightcoreEnabled == true)
            {
                result.Player.Filters.Timescale = new Lavalink4NET.Filters.TimescaleFilterOptions(
                    Speed: 1.25f,
                    Pitch: 1.25f,
                    Rate: 1.0f
                );
                await result.Player.Filters.CommitAsync();
            }

            return result.Player;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting player for guild {GuildId}", guildId);
            return null;
        }
    }
}
