using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

public class JSONManager
{
	private readonly ConcurrentDictionary<ulong, ServerInformation> _servers = new();
	private readonly SemaphoreSlim _fileLock = new(1, 1);
	private readonly ILogger<JSONManager> _logger;
	private const string ConfigPath = "servers.json";

	public IEnumerable<ServerInformation> Servers => _servers.Values;

	public JSONManager(ILogger<JSONManager> logger)
	{
		_logger = logger;
	}

	public async Task LoadAsync()
	{
		await _fileLock.WaitAsync();
		try
		{
			if (!File.Exists(ConfigPath))
			{
				_logger.LogWarning("servers.json not found, creating empty config");
				await File.WriteAllTextAsync(ConfigPath, "[]");
				return;
			}

			string json = await File.ReadAllTextAsync(ConfigPath);
			var servers = JsonSerializer.Deserialize<List<ServerInformation>>(json);

			if (servers != null)
			{
				_servers.Clear();
				foreach (var server in servers)
				{
					_servers[server.Id] = server;
				}
			}

			_logger.LogInformation("Loaded {Count} server configurations", _servers.Count);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to load servers.json");
		}
		finally
		{
			_fileLock.Release();
		}
	}

	public async Task SaveAsync()
	{
		await _fileLock.WaitAsync();
		try
		{
			var options = new JsonSerializerOptions { WriteIndented = true };
			string json = JsonSerializer.Serialize(_servers.Values.ToList(), options);
			await File.WriteAllTextAsync(ConfigPath, json);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to save servers.json");
		}
		finally
		{
			_fileLock.Release();
		}
	}

	public ServerInformation? GetServer(ulong serverId)
	{
		_servers.TryGetValue(serverId, out var server);
		return server;
	}

	public ServerInformation GetOrCreateServer(ulong serverId, ulong channelId)
	{
		return _servers.AddOrUpdate(
			serverId,
			_ => new ServerInformation { Id = serverId, MusicChannelId = channelId },
			(_, existing) =>
			{
				existing.MusicChannelId = channelId;
				return existing;
			});
	}

	public void UpdateServer(ServerInformation server)
	{
		_servers[server.Id] = server;
	}
}

