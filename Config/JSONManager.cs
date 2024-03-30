using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

public class JSONManager
{
	public List<ServerInformation> Servers;

	public JSONManager() { Servers = new List<ServerInformation>(); }

	public async Task ReadJSON()
	{
		// PLEASE NOTE that you have to copy your "config.json" file (with your token & prefix) over to
		// the /bin/Debug folder of your solution, else this won't work

		using (StreamReader sr = new StreamReader("servers.json", new UTF8Encoding(false)))
		{
			string json = await sr.ReadToEndAsync(); //Reading whole file
			List<ServerInformation> jsonServers = JsonConvert.DeserializeObject<List<ServerInformation>>(json); //Deserialising file into the ServerInformation structure

			if (jsonServers != null)
				Servers = jsonServers;
		}
	}

	public async Task UpdateServerList()
	{
		string jsonString = JsonConvert.SerializeObject(Servers);

		using (StreamWriter outputFile = new StreamWriter("servers.json"))
		{
			await outputFile.WriteAsync(jsonString);
		}
	}

	public ServerInformation ServerExists(string serverId)
	{
		foreach (ServerInformation server in Servers)
		{
			if (server.Id == serverId)
				return server;
		}

		return null;
	}
}

