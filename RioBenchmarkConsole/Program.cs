using rbcl.network;

var server = new TcpServer("127.0.0.1");


Console.WriteLine("Starting server");
await server.Start();
Console.WriteLine("Server has been stopped");