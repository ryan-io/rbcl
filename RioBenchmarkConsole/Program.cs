using rbcl.network;
using System.Net;

var server = new TcpServer("127.0.0.1");
Console.WriteLine("Starting server");

var startTask = server.Start();

await Task.Delay(2000);
server.Connect(IPAddress.Parse("127.0.0.1"));

var logicTask = Task.Run(() =>
{
	while (true)
	{
		var input = Console.ReadLine();

		if (string.IsNullOrWhiteSpace(input))
		{
			continue;
		}

		if (input == "exit")
		{
			break;
		}

		var t = server?.ClientMap;
		server?.BroadcastAll(input);
		Console.WriteLine($"Broadcasting: {input}");
	}

	server?.Dispose();
});

await Task.WhenAll(startTask, logicTask);

Console.WriteLine("Server has been stopped");