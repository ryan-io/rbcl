using System.Text;
using Microsoft.Extensions.Logging;
using rbcl;
using rbcl.naive;
using rbcl.network;
using riolog;

var array = NaiveArrayPool<byte>.Rent(89);
Console.WriteLine(array.Length);
return 0;

async Task RunServer()
{
	var logger = new Logger();
	var cts = new CancellationTokenSource();
	using var server = new TcpServer("127.0.0.1", logger: logger);
	server.Start(cts.Token);

	var reader = new ConsoleStreamReader();
	reader.InputReceived += TestExit;
	var readTask = reader.Start(cts.Token);

	var blocker = new ResponsiveBlock();
	var blockTask = blocker.Wait(() => cts.IsCancellationRequested);

	await Task.WhenAll(readTask, blockTask);

	logger.CloseAndFlush();

	void TestExit (string? msg) {
		if (msg == "exit") {
			cts.Cancel();
		}
	}
}

async Task RunClient () {
	var logger = new Logger();
	var cts = new CancellationTokenSource();

	using var client = new TcpClientExtendedTest("127.0.0.1", 10000);

	var reader = new ConsoleStreamReader();
	reader.InputReceived += TestExit;
	var readTask = reader.Start(cts.Token);

	var blocker = new ResponsiveBlock();
	var blockTask = blocker.Wait(() => cts.IsCancellationRequested);

	await Task.WhenAll(readTask, blockTask);

	logger.CloseAndFlush();

	void TestExit (string? msg) {
		if (msg == "exit") {
			cts.Cancel();
		}
		else
		{
			client.SendData(Encoding.UTF8.GetBytes(msg));
		}	
	}
}


class Logger : ITcpLogger {
	public void Log (string message) {
		_log.LogDebug(message);
	}

	public void LogException (Exception exception) {
		_log.LogError(exception.Message);
	}

	public void LogTrace (string message) {
		_log.LogTrace(message);
	}

	public void LogWarning (string message) {
		_log.LogWarning(message);
	}

	public void CloseAndFlush () {
		_log.CloseAndFlush();
	}

	private readonly ILogger _log = InternalLogFactory.SetupAndStartAsLogger(Output.Console);
}

//using System.Net.Sockets;
//using System.Text;

//// this is our client

//var cts = new CancellationTokenSource();
//var client = new TcpClient("127.0.0.1", 10000);

//var msg = "Connecting to server...";
//var stream = client.GetStream();
//var byteStream = Encoding.ASCII.GetBytes(msg);

//// send a message to the server
//await stream.WriteAsync(byteStream, 0, byteStream.Length, cts.Token);

//// receive a message from the server
//var buffer = new byte[1024];
//var received = await stream.ReadAsync(buffer, cts.Token);
//var message = Encoding.ASCII.GetString(buffer, 0, received);
//Console.WriteLine($"Received {message}");

//stream.Close();
//client.Close();

////var cts = new CancellationTokenSource();
////var inputTask = PollInput(cts.Token);
////var outputTask = PollOutput(cts.Token);

////await Task.WhenAll(inputTask, outputTask);

//////socket.Shutdown(SocketShutdown.Both);
////socket.Close();

//return 0;

//async Task PollInput (CancellationToken token)
//{
//	while (!token.IsCancellationRequested)
//	{
//		var input = Console.ReadLine();
//		if (string.IsNullOrWhiteSpace(input))
//		{
//			continue;
//		}

//		TcpClient client;
//		if (input == "exit")
//		{
//			cts.Cancel();
//			break;
//		}

//		var buffer = Encoding.UTF8.GetBytes(input);
//		try
//		{
//			_ = await socket.SendAsync(buffer, SocketFlags.None, token);
//		}
//		catch (SocketException ex)
//		{
//			Console.WriteLine($"SocketException: {ex.Message}");
//			break;
//		}
//		catch (Exception ex)
//		{
//			Console.WriteLine($"Exception: {ex.Message}");
//			break;
//		}
//	}
//}

//async Task PollOutput (CancellationToken token)
//{
//	while (!token.IsCancellationRequested)
//	{
//		var buffer = new byte[1024];
//		var received = await socket.ReceiveAsync(buffer, SocketFlags.None, token);
//		var message = Encoding.UTF8.GetString(buffer, 0, received);
//		Console.WriteLine(message);
//	}
//}


//using rbcl.network;
//using System.Net;

//var server = new TcpServer("127.0.0.1");
//Console.WriteLine("Starting server");

//var startTask = server.Start();

//await Task.Delay(2000);
//server.Connect(IPAddress.Parse("127.0.0.1"));

//var logicTask = Task.Run(() =>
//{
//	while (true)
//	{
//		var input = Console.ReadLine();

//		if (string.IsNullOrWhiteSpace(input))
//		{
//			continue;
//		}

//		if (input == "exit")
//		{
//			break;
//		}

//		var t = server?.ClientMap;
//		server?.BroadcastAll(input);
//		Console.WriteLine($"Broadcasting: {input}");
//	}

//	server?.Dispose();
//});

//await Task.WhenAll(startTask, logicTask);

//Console.WriteLine("Server has been stopped");