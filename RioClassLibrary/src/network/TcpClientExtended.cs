using System.Buffers;
using System.Net.Sockets;
using System.Text.Json;

namespace rbcl.network;

public class TcpClientExtended : IDisposable
{
	#region Events

	public event Action<BroadCastPacket>? Received;

	#endregion

	#region Public Methods

	public void Start (CancellationToken? externalToken = default)
	{
		if (externalToken != null)
		{
			_cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, externalToken.Value);
		}

		_reader.InputReceived += OnInputReceived;

		Task.Run(() =>
		{
			while (!_cts.IsCancellationRequested)
			{
				var buffer = _memory.Memory;
				var stream = _client.GetStream();
				var received = stream.Read(buffer.Span);

				if (received == 0)
				{
					continue;
				}

				var toParse = buffer.Slice(0, received);
				var packet = JsonSerializer.Deserialize<BroadCastPacket>(toParse.Span);
				Received?.Invoke(packet);
			}
		}, _cts.Token);
	}

	public void Dispose ()
	{
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
	public TcpClientExtended (string address, int port, ITcpLogger? logger = default)
	{
		_logger = logger;
		_client = new TcpClient(address, port);
		_memory = MemoryPool<byte>.Shared.Rent(BufferSize);
	}

	#endregion

	#region Private Methods

	void OnInputReceived (string? message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
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