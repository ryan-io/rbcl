using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

// TODO: add OnClientConnected and OnClientDisconnected events

namespace rbcl.network
{
	/// <summary>
	/// Data structure for containing a socket and the associated cancellation token
	/// </summary>
	public class Connection
	{
		public Socket Socket { get; }

		public Thread Handler { get; }

		public CancellationTokenSource CancellationTokenSource { get; } = new();

		public Connection (Socket socket, Thread handler)
		{
			Socket = socket;
			Handler = handler;
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
		public const string ServerAlreadyStarted = "Server has already been started.";

		public static string BuildConnectionMsg (IPAddress address) => $"Client {address.ToString()} connected to the server.";

		public static class Warning
		{
			public const string ConnectionAlreadyOpened =
				"Server is already bound to an endpoint and is accepting clients.";
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
	/// A class to allow TCP communication between a server and client
	/// This class is disposable, and it should have it's Dispose method called when appropriate for your application
	/// A TcpServer object may be cancelled from any thread; this will have the effect of interrupting, follow by closing all connections
	/// </summary>
	public class TcpServer : IDisposable
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
		/// The endpoint of the server (IP address and port)
		/// </summary>
		public IPEndPoint Endpoint { get; private set; }

		/// <summary>
		/// Checks if a client is connected to the server
		/// </summary>
		/// <returns>True is open connections finds address, otherwise false</returns>
		public bool IsConnected (IPAddress address)
		{
			return _connections.ContainsKey(address);
		}

		public void Broadcast ()
		{

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
				lock (_mutex)
				{
					_cancellationTokenSource.TryReset();
				}

				UserHasStarted = true;

				// start each thread
				foreach (var connection in _connections)
				{
					connection.Value.Handler.Start();
				}

				await Task.WhenAll([ListenForConnect(), ListenForSend(), ListenForReceive()]);
			}
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

			UserHasStarted = false;

			foreach (var connection in _connections)
			{
				connection.Value.Socket.Shutdown(SocketShutdown.Both);
				connection.Value.Socket.Close();                                    // invokes Dispose on the socket
				connection.Value.CancellationTokenSource.Cancel();
			}

			_socket.Shutdown(SocketShutdown.Both);
			_socket.Close();                                                        // invokes Dispose on the socket
			_isDisposed = true;
		}

		#endregion

		#region Constructor

		/// <param name="address">IP address endpoint</param>
		/// <param name="port">Port to open the server on; default 80</param>
		/// <param name="logger">Optional logger</param>
		public TcpServer (string address, int port = 80, ITcpLogger? logger = default)
		{
			Endpoint = new IPEndPoint(IPAddress.Parse(address), port);

			_connections = new Connections();
			_cancellationTokenSource = new CancellationTokenSource();
			_logger = logger;
			_socket = new Socket(Endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			_socket.Bind(Endpoint);
			_socket.Listen(Backlog);
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
						_logger?.LogWarning(TcpMessages.Error.BuildAlreadyConnectedMsg(endpoint.Address));
						continue;
					}

					// new up a new client handler
					var clientThread = new Thread(BuildClientThread);
					var connection = new Connection(tentative, clientThread);
					var wasAdded = ThreadSafeRegister(endpoint.Address, connection);

					if (!wasAdded)  // unnecessary robustness here?
					{
						_logger?.LogWarning(TcpMessages.Error.BuildAlreadyConnectedMsg(endpoint.Address));
						continue;
					}

					_logger?.Log(TcpMessages.BuildConnectionMsg(endpoint.Address));
					Connected?.Invoke(connection);

					// local utility method to build a client thread
					void BuildClientThread (object? x)
					{
						int counter = 0;
						// need new client object to begin send/receive operations
						while (counter < 1_000)
						{
							Thread.Sleep(500);
							Console.WriteLine("Putting thread to sleep for 0.5 sec.");
							counter++;
						}

						Console.WriteLine("Thread is done.");
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
				await Task.Delay(TimeSpan.FromSeconds(1));
				Console.WriteLine("Server is listening for messages.");
			}
		}

		async Task ListenForReceive ()
		{
			while (Running)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(500));
				Console.WriteLine("Server is receiving messages.");
			}
		}

		/// <summary>
		/// Utility method for adding a connection to the concurrent dictionary
		/// </summary>
		bool ThreadSafeRegister (IPAddress ipAddress, Connection connection)
		{
			return _connections.TryAdd(ipAddress, connection);
		}

		bool _isDisposed;

		readonly CancellationTokenSource _cancellationTokenSource;

		readonly Socket _socket;

		readonly ITcpLogger? _logger;

		readonly Connections _connections;

		readonly object _mutex = new();

		const int Backlog = 100;

		#endregion
	}

	/// <summary>
	/// A class to query a sever via TCP to send and receive data
	/// When connecting a client socket to a server socket, the client will use an IPEndPoint object
	///		to specify the network address of the server.
	/// </summary>
	public class TcpClient
	{
		/// <summary>
		/// A unique identifier for the client
		/// </summary>
		public string Identifier { get; private set; }

		/// <summary>
		/// The IP address of the server to connect to
		/// </summary>
		/// <param name="endpoint"></param>
		public void Connect (IPEndPoint endpoint)
		{
			//var bytes = Encoding.ASCII.GetBytes(ipAddress).AsSpan();
		}

		#region Constructor

		/// <param name="identifier">A unique identifier for the client</param>
		public TcpClient (string identifier)
		{
			Identifier = identifier;
		}

		#endregion
	}
}
