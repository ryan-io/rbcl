using System.Buffers;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace rbcl.network;

public class TcpClientExtendedTest : IDisposable {
	public class Sender : IDisposable {
		public void QueueData (byte[] data) {
			for (int i = 0; i < data.Length; i++) {
				_buffer.Memory.Span[i] = data[i];
			}

			_writeRequested = true;
		}

		public Sender (NetworkStream stream) {
			_stream = stream;
			_thread = new Thread(Send);
			_thread.Start();
		}

		public void Dispose () {
			Dispose(true);
			GC.SuppressFinalize(this); 
		}

		void Send () {
			if (_cts.IsCancellationRequested)
			{
				return;
			}

			if (_writeRequested) {
				_stream.Write(_buffer.Memory.Span);
				_writeRequested = false;
			}
		}

		void Dispose (bool disposing) {
			if (!_disposed) {
				if (disposing) {
					_cts.Cancel();
					_cts.Dispose();
					_stream.Close();
					_buffer.Dispose();
				}

				_disposed = true;
			}
		}

		IMemoryOwner<byte> _buffer = MemoryPool<byte>.Shared.Rent(1024);
		bool _disposed;
		CancellationTokenSource _cts = new();
		private NetworkStream _stream;
		bool _writeRequested;
		readonly Thread _thread;
	}

	public class Receiver : IDisposable {
		public event EventHandler<DataReceivedEventArgs> DataReceived;

		public void Dispose () {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		public Receiver (NetworkStream stream) {
			_stream = stream;
			_thread = new Thread(Receive);
			_thread.Start();
		}

		void Receive () {
			using IMemoryOwner<byte> buffer = MemoryPool<byte>.Shared.Rent(1024);

			var bytesReceived = _stream.Read(buffer.Memory.Span);

			if (_cts.IsCancellationRequested) {
				return;
			}
			if (bytesReceived == 0) {
				Receive();
			}

			// from index '0' to bytesRead - 1
			var data = buffer.Memory[..bytesReceived].ToArray();
			Console.WriteLine($"Received: {Encoding.UTF8.GetString(data)}");
			Receive();
		}

		void Dispose (bool disposing) {
			if (!_disposed) {
				if (disposing) {
					_cts.Cancel();
					_cts.Dispose();
					_stream.Close();
				}

				_disposed = true;
			}
		}

		bool _disposed;
		private NetworkStream _stream;
		private Thread _thread;
		CancellationTokenSource _cts = new();
	}

	public event EventHandler<DataReceivedEventArgs> DataReceived;

	public void SendData (byte[] data) {
		_sender.QueueData(data);
	}

	public TcpClientExtendedTest (string ip, int port) {
		_client = new TcpClient(ip, port);
		_sender = new Sender(_client.GetStream());
		_receiver = new Receiver(_client.GetStream());

		_receiver.DataReceived += OnDataReceived;
	}

	public void Dispose () {
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	void OnDataReceived (object? sender, DataReceivedEventArgs e) {
		DataReceived?.Invoke(sender, e);
		Console.WriteLine("Received some data!");
	}

	void Dispose (bool disposing) {
		if (!_disposed) {
			if (disposing) {
				_cts.Cancel();
				_cts.Dispose();
				_client.Dispose();
				_sender.Dispose();
				_receiver.Dispose();
			}

			_disposed = true;
		}
	}

	private TcpClient _client;
	private bool _disposed;
	CancellationTokenSource _cts = new();
	private Sender _sender;
	private Receiver _receiver;
}

public class TcpClientExtended : IDisposable {
	#region Events

	public event Action<BroadCastPacket>? Received;

	#endregion

	#region Public Methods

	public void Start (CancellationToken? externalToken = default) {
		if (externalToken != null) {
			_cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, externalToken.Value);
		}

		_reader.InputReceived += OnInputReceived;

		Task.Run(() => {
			while (!_cts.IsCancellationRequested) {
				var buffer = _memory.Memory;
				var stream = _client.GetStream();
				var received = stream.Read(buffer.Span);

				if (received == 0) {
					continue;
				}

				var toParse = buffer.Slice(0, received);
				var packet = JsonSerializer.Deserialize<BroadCastPacket>(toParse.Span);
				Received?.Invoke(packet);
			}
		}, _cts.Token);
	}

	public void Dispose () {
		_reader.Stop();
		_reader.Dispose();

		_cts.Cancel();
		_cts.Dispose();

		_client.Close();
		_memory.Dispose();
	}

	#endregion

	#region Constructor

	/// <param name="address">The IP address to connect to</param>
	/// <param name="port">The port number to connect to</param>
	/// <param name="logger">Optional logger</param>
	public TcpClientExtended (string address, int port, ITcpLogger? logger = default) {
		_logger = logger;
		_client = new TcpClient(address, port);
		_memory = MemoryPool<byte>.Shared.Rent(BufferSize);
	}

	#endregion

	#region Private Methods

	void OnInputReceived (string? message) {
		if (string.IsNullOrWhiteSpace(message)) {
			return;
		}

		var packet = new BroadCastPacket(message, _client.GetHashCode());
		var data = JsonSerializer.SerializeToUtf8Bytes(packet);

		// broadcast the message from this client to all other clients
		_client.GetStream().Write(data, 0, data.Length);
	}

	#endregion

	#region Private Fields

	CancellationTokenSource _cts = new();

	#endregion

	#region Readonly & Constant Fields

	private readonly TcpClient _client;

	private readonly IMemoryOwner<byte> _memory;

	private readonly ConsoleStreamReader _reader = new();

	private readonly ITcpLogger? _logger;

	private const int BufferSize = 1024;

	#endregion
}