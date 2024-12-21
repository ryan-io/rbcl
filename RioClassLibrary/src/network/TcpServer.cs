using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace rbcl.network
{
	/// <summary>
	/// Data structure for containing a socket and the associated cancellation token
	/// </summary>
	public class Connection
	{
		/// <summary>
		/// Persistent segment of memory for the connection buffer
		/// Requires disposal
		/// </summary>
		public IMemoryOwner<byte> MemoryOwner { get; }

		/// <summary>
		/// The server that the client is connected to allowing for access to the 'Broadcast' methods
		/// </summary>
		public ITcpCom Server { get; }

		/// <summary>
		/// The client socket
		/// </summary>
		public Socket Socket { get; }

		/// <summary>
		/// The thread that handles the client
		/// </summary>
		public Thread Handler { get; }

		/// <summary>
		/// Utility method for easily accessing the buffer span
		/// </summary>
		public Span<byte> Buffer => MemoryOwner.Memory.Span;

		/// <summary>
		/// Size of the buffer to instantiate
		/// </summary>
		public const int BufferSize = 1024;

		/// <summary>
		/// For cancelling the client connection thread
		/// </summary>
		public CancellationTokenSource CancellationTokenSource { get; } = new();

		/// <summary>
		/// Constructor
		/// </summary>
		public Connection (Socket socket, Thread handler, ITcpCom server)
		{
			MemoryOwner = MemoryPool<byte>.Shared.Rent(BufferSize);
			Socket = socket;
			Handler = handler;
			Server = server;
		}
	}

	// Each client that wants to connect to a server will have a unique identifier (IP address)
	// and a socket to communicate with the server
	internal sealed class Connections : ConcurrentDictionary<IPAddress, Connection> { }

	/// <summary>
	/// Contains various logging messages for a TCP application
	/// </summary>
	internal static class TcpMessages
	{
		public const string ServerDisposed = "Server resources have been released.";

		public const string ServerAlreadyStarted = "Server has already been started.";

		public const string ServerIsNotRunning = "Server is not currently running. Invoke 'Start()' to begin.";

		public static string BuildConnectionMsg (IPAddress address) => $"Client {address.ToString()} connected to the server.";

		public static class Warning
		{
			public const string ConnectionAlreadyOpened =
				"Server is already bound to an endpoint and is accepting clients.";

			public const string CannotBroadcast = "Server is not running. Cannot broadcast message to client(s).";

			public static string BuildNoIpMsg (IPAddress address) => $"Cannot find client with IP address {address.ToString()}.";
		}

		public static class Error
		{
			public const string ConnectionNotStart =
				"Server has not been start. Please invoked 'Start()' on the server object.";

			public const string NoEndPointBound = "No endpoint was bound to the client socket.";

			public static string BuildAlreadyConnectedMsg (IPAddress address) => $"Client {address.ToString()} is already connected to the server.";
		}
	}

	/// <summary>
	/// Abstraction for logging in a TCP application
	/// </summary>
	public interface ITcpLogger
	{
		void Log (string message);
		void LogException (Exception exception);
		void LogTrace (string message);
		void LogWarning (string message);
	}

	/// <summary>
	/// Contract to implement a TCP communication interface
	/// </summary>
	public interface ITcpCom
	{
		/// <summary>
		/// Send a message to all connected clients
		/// </summary>
		void BroadcastAll (string data);

		// Send a message to a specific client
		void Broadcast (string data, IPAddress client);
	}

	/// <summary>
	/// A class to allow TCP communication between a server and client
	/// This class is disposable, and it should have it's Dispose method called when appropriate for your application
	/// A TcpServer object may be cancelled from any thread; this will have the effect of interrupting, follow by closing all connections
	/// Invoke the <see cref="Dispose"/> method to close the server, all connections, and resources
	/// </summary>
	public sealed class TcpServer : ITcpCom, IDisposable
	{
		#region Public

		/// <summary>
		/// Emitted when a new client connects to the server
		/// </summary>
		public event Action<Connection>? Connected;

		/// <summary>
		/// Emitted when a client disconnects from the server
		/// </summary>
		public event Action<IPAddress>? Disconnected;

		/// <summary>
		/// A readonly collection of all clients connected to the server
		/// </summary>
		public IReadOnlyDictionary<IPAddress, Connection> ClientMap => _connections;

		/// <summary>
		/// The endpoint of the server (IP address and port)
		/// </summary>
		public IPEndPoint Endpoint { get; }

		/// <summary>
		/// Port number of the server
		/// </summary>
		public int Port => Endpoint.Port;

		/// <summary>
		/// Checks if a client is connected to the server
		/// </summary>
		/// <returns>True is open connections finds address, otherwise false</returns>
		public bool IsConnected (IPAddress address)
		{
			return _connections.ContainsKey(address);
		}

		public void Connect (IPAddress ipAddress)
		{
			var socket = new Socket(Endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			socket.Connect(Endpoint);
			//var connection = new Connection(_socket, build, this);
		}

		/// <inheritdoc/>>
		public void BroadcastAll (string data)
		{
			_connections.First().Value.Socket.Send(Encoding.ASCII.GetBytes(data));

			//foreach (var ipAddress in _connections.Keys)
			//{
			//	// there are a couple of checks within this method that may appear to be redundant and/or unnecessary
			//	// these checks should remain in place in case the server is stopped from another thread
			//	Broadcast(data, ipAddress);
			//}
		}

		/// <inheritdoc/>>
		public void Broadcast (string data, IPAddress clientIp)
		{
			if (!Running)
			{
				_logger?.LogWarning(TcpMessages.Warning.CannotBroadcast);
				return;
			}

			var state = _connections.TryGetValue(clientIp, out var clientConnection);

			if (!state || clientConnection == null)
			{
				_logger?.LogWarning(TcpMessages.Warning.BuildNoIpMsg(clientIp));
				return;
			}

			// this is a blocking call, okay since 
			clientConnection.Socket.SendAsync(Encoding.ASCII.GetBytes(data));
		}

		/// <summary>
		/// Starts the server and allows new clients to connect
		/// </summary>
		public async Task Start ()
		{
			if (UserHasStarted)
			{
				_logger?.LogWarning(TcpMessages.ServerAlreadyStarted);
			}
			else
			{
				//TODO: this is throwing an exception when connecting new clients to the server and requires investigation.
				_socket.Listen(Backlog);

				lock (_mutex)
				{
					_cancellationTokenSource.TryReset();
				}

				UserHasStarted = true;
				await Task.WhenAll([ListenForConnect(), ListenForReceive()]);
			}
		}

		/// <summary>
		/// Stops the server and prevents new clients from connecting
		/// Invoked <see cref="Start"/> to once again accept new clients
		/// </summary>
		public void Pause ()
		{
			if (!Running)
			{
				_logger?.LogWarning(TcpMessages.ServerIsNotRunning);
				return;
			}

			_socket.Shutdown(SocketShutdown.Both);
		}

		/// <summary>
		/// Disposes all sockets and closes the server
		/// </summary>
		/// <exception cref="ObjectDisposedException">Thrown if object is disposed</exception>
		public void Dispose ()
		{
			if (_isDisposed)
			{
				var exception = new ObjectDisposedException(nameof(TcpServer));
				_logger?.LogException(exception);
				throw exception;
			}

			if (Running)
			{
				Pause();
			}

			UserHasStarted = false;

			foreach (var connection in _connections)
			{
				connection.Value.Socket.Close();                                    // invokes Dispose on the socket
				connection.Value.CancellationTokenSource.Cancel();
				connection.Value.MemoryOwner.Dispose();
			}

			_socket.Disconnect(false);
			_socket.Close();                                                        // invokes Dispose on the socket
			_isDisposed = true;
			_logger?.LogTrace(TcpMessages.ServerDisposed);
		}

		#endregion

		#region Constructor

		/// <param name="address">IP address endpoint</param>
		/// <param name="port">Port to open the server on; default 80</param>
		/// <param name="logger">Optional logger</param>
		public TcpServer (string address, int port = 80, ITcpLogger? logger = default)
		{
			Endpoint = new IPEndPoint(IPAddress.Parse(address), port); //IPAddress.Parse(address)

			_connections = new Connections();
			_cancellationTokenSource = new CancellationTokenSource();
			_logger = logger;
			_socket = new Socket(Endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			_socket.Bind(Endpoint);
		}

		#endregion

		#region Private

		/// <summary>
		/// Checks if the server is running
		/// </summary>
		bool Running => !_cancellationTokenSource.IsCancellationRequested && UserHasStarted;

		/// <summary>
		/// Flag for tracking whether the server owner has started the server
		/// </summary>
		bool UserHasStarted { get; set; }

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		async Task ListenForConnect ()
		{
			while (Running)
			{
				var tentative = await _socket.AcceptAsync(_cancellationTokenSource.Token);

				if (!Running)
				{
					break;
				}

				var endpoint = (IPEndPoint?)(tentative?.LocalEndPoint);

				try
				{
					if (endpoint is null || tentative is null)
					{
						throw new SocketException(-1, TcpMessages.Error.NoEndPointBound);
					}

					if (IsConnected(endpoint.Address))
					{
						tentative.Close();
						_logger?.LogWarning(TcpMessages.Error.BuildAlreadyConnectedMsg(endpoint.Address));
						continue;
					}

					// new up a new client handler
					var clientThread = new Thread(RunClient);
					var connection = new Connection(tentative, clientThread, this);
					var wasAdded = ThreadSafeRegister(endpoint.Address, connection);

					if (!wasAdded)  // unnecessary robustness here?
					{
						tentative.Close();
						_logger?.LogWarning(TcpMessages.Error.BuildAlreadyConnectedMsg(endpoint.Address));
						continue;
					}

					_logger?.Log(TcpMessages.BuildConnectionMsg(endpoint.Address));

					lock (_mutex) // lock mutex and fire the event, ensure no wildcard subscribers
					{
						Connected?.Invoke(connection);
					}

					// start the client thread
					clientThread.Start(connection);
					//await connection.Socket.SendAsync(connection.MemoryOwner.Memory, connection.CancellationTokenSource.Token);


					// local utility method to build a client thread
					void RunClient (object? parameter)
					{
						if (parameter == null)
						{
							// TODO: more appropriate handling
							return;
						}

						var client = (Connection)parameter;

						if (client == null)
						{
							throw new NullReferenceException("Client passed to work thread was null.");
						}

						Console.WriteLine("Client has been connected.");
						while (!client.CancellationTokenSource.IsCancellationRequested)
						{
							var bytesRead = client.Socket.Receive(client.Buffer);
							var data = Encoding.ASCII.GetString(client.Buffer);
							Console.WriteLine(data);
						}
						Console.WriteLine("Client has been connected.");

					}
				}
				catch (SocketException e)
				{
					_logger?.LogException(e);
				}
			}
		}

		async Task ListenForSend ()
		{
			while (Running)
			{
			}
		}

		async Task ListenForReceive ()
		{
			while (Running)
			{
				await _socket.ReceiveAsync(_memory);

			}
		}

		/// <summary>
		/// Utility method for adding a connection to the concurrent dictionary
		/// </summary>
		bool ThreadSafeRegister (IPAddress ipAddress, Connection connection)
		{
			return _connections.TryAdd(ipAddress, connection);
		}

		Memory<byte> _memory = new();

		bool _isDisposed;

		readonly CancellationTokenSource _cancellationTokenSource;

		readonly Socket _socket;

		readonly ITcpLogger? _logger;

		readonly Connections _connections;

		readonly object _mutex = new();

		const int Backlog = 100;

		#endregion
	}

	///// <summary>
	///// A class to query a sever via TCP to send and receive data
	///// When connecting a client socket to a server socket, the client will use an IPEndPoint object
	/////		to specify the network address of the server.
	///// </summary>
	//public class TcpClient
	//{
	//	/// <summary>
	//	/// A unique identifier for the client
	//	/// </summary>
	//	public string Identifier { get; private set; }

	//	/// <summary>
	//	/// The IP address of the server to connect to
	//	/// </summary>
	//	/// <param name="endpoint"></param>
	//	public void Connect (IPEndPoint endpoint)
	//	{
	//		//var bytes = Encoding.ASCII.GetBytes(ipAddress).AsSpan();
	//	}

	//	#region Constructor

	//	/// <param name="identifier">A unique identifier for the client</param>
	//	public TcpClient (string identifier)
	//	{
	//		Identifier = identifier;
	//	}

	//	#endregion
	//}
}