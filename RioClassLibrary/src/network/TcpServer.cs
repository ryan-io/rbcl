using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace rbcl.network
{
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
	/// Invoke the <see cref="Dispose"/> method to close the server, all connections, and resources
	/// </summary>
	public sealed class TcpServer : IDisposable
	{
		#region Events

		/// <summary>
		/// Emitted when a new client connects to the server
		/// </summary>
		public event Action<TcpClient>? Connected;

		#endregion

		#region Public - Properties, Fields, Static and Constant

		/// <summary>
		/// Ip address of the server
		/// </summary>
		public string Ip { get; private set; }

		/// <summary>
		/// Port number of the server
		/// </summary>
		public int Port { get; private set; }

		/// <summary>
		/// Returns true if the server is accepting new clients, otherwise false
		/// </summary>
		public bool Started { get; private set; }

		#endregion

		#region Public - Methods

		/// <summary>
		/// Starts the server and allows new clients to connect
		/// </summary>
		public void Start (CancellationToken? externalToken = default)
		{
			if (Started)
			{
				_logger?.Log(Message.ServerAlreadyStarted);
				return;
			}

			if (externalToken != null)
			{
				_cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, externalToken.Value);
			}

			_listener.Start();

			try
			{
				// start the server in a new thread
				Task.Run(Accept, _cts.Token);
				Task.Run(TestOut, _cts.Token);
			}
			catch (TaskCanceledException e) { _logger?.LogException(e); }
			catch (SocketException e) { _logger?.LogException(e); }

			_logger?.Log(Message.ServerStarted);
			Started = true;
		}

		private void TestOut ()
		{
			var client = new TcpClientExtended(Ip, Port);
			client.Received += packet =>
			{
				_logger?.Log($"Received packet: {packet.Data}");
			};

			var thread = new Thread(() =>
			{
				client.Start(_cts.Token);
			});

			thread.Start();
		}

		/// <summary>
		/// Used to stop the server and free allocated resources
		/// </summary>
		/// <exception cref="ObjectDisposedException">Thrown if object is disposed</exception>
		public void Dispose ()
		{
			if (!Started)
			{
				_logger?.LogWarning(Message.Warning.ServerRunning);
				return;
			}

			if (_isDisposed)
			{
				var exception = new ObjectDisposedException(nameof(TcpServer));
				_logger?.LogException(exception);
				throw exception;
			}

			_logger?.LogTrace(Message.ServerDisposed);

			foreach (var client in _connections.Values)
			{
				client.Close();
			}

			_listener.Stop();
			_cts?.Dispose();
			_isDisposed = true;
		}

		#endregion

		#region Constructor

		/// <param name="address">IP address endpoint</param>
		/// <param name="port">Port to open the server on; default 10000</param>
		/// <param name="logger">Optional logger</param>
		public TcpServer (string address, int port = 10000, ITcpLogger? logger = default)
		{
			Ip = address;
			Port = port;
			_logger = logger;
			_listener = new TcpListener(IPAddress.Any, port);
		}

		#endregion

		#region Private - Propertes & Fields

		CancellationTokenSource _cts = new();

		bool _isDisposed;

		#endregion

		#region Private - Methods

		/// <summary>
		/// Establishes a connection with a new client
		/// </summary>
		void Accept ()
		{
			try
			{
				while (!_cts.IsCancellationRequested)
				{
					var client = _listener.AcceptTcpClient();
					_connections.TryAdd(client.GetHashCode(), client);
					_logger?.Log(Message.BuildNewClientConnect(client.GetHashCode()));
					Connected?.Invoke(client);
				}
			}
			catch (SocketException e) { _logger?.LogException(e); }
		}

		#endregion

		#region Private - Static, Constant & Readonly Fields

		readonly ConcurrentDictionary<int, TcpClient> _connections = new();

		readonly TcpListener _listener;

		readonly ITcpLogger? _logger;

		#endregion

		#region Tcp Messages

		/// <summary>
		/// Contains various logging messages for a TCP application
		/// </summary>
		static class Message
		{
			public const string ServerStarted = "Tcp Server has been started";

			public const string ServerDisposed = "Server resources have been released.";

			public const string ServerAlreadyStarted = "Server has already been started.";

			public const string ServerIsNotRunning = "Server is not currently running. Invoke 'Start()' to begin.";

			public static string BuildNewClientConnect (int hash) => $"Client {hash.ToString()} has joined the server";

			public static class Warning
			{
				public const string ServerRunning = "Server is currently stopped.";

				public const string ConnectionAlreadyOpened =
					"Server is already bound to an endpoint and is accepting clients.";

				public const string CannotBroadcast = "Server is not running. Cannot broadcast message to client(s).";

				public static string BuildNoIpMsg (IPAddress address) =>
					$"Cannot find client with IP address {address.ToString()}.";
			}

			public static class Error
			{
				public const string ConnectionNotStart =
					"Server has not been start. Please invoked 'Start()' on the server object.";

				public const string NoEndPointBound = "No endpoint was bound to the client socket.";

				public static string BuildAlreadyConnectedMsg (IPAddress address) =>
					$"Client {address.ToString()} is already connected to the server.";
			}
		}

		#endregion
	}
}