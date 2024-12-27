namespace rbcl;

/// <summary>
/// A class that puts a wrapper around the standard input and asynchronously reads from it
/// </summary>
public sealed class ConsoleStreamReader : IDisposable
{
	#region Public - Properties, Fields & Events

	/// <summary>
	/// Returns true if the reader is currently running, otherwise false
	/// </summary>
	public bool IsRunning { get; private set; }

	public event Action<string?>? InputReceived;

	#endregion

	#region Public - Methods

	/// <summary>
	/// Starts the reader
	/// Any input received by the consumer will be passed to the <see cref="InputReceived"/> event
	/// </summary>
	public async Task Start (CancellationToken? token = default)
	{
		if (IsRunning)
		{
			return;
		}

		IsRunning = true;

		if (_cts.IsCancellationRequested)
		{
			_cts.TryReset();
		}
		else
		{
			_cts = new CancellationTokenSource();
		}

		if (token != null)
		{
			token = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, token.Value).Token;
		}
		else
		{
			token = _cts.Token;
		}

		var realToken = (CancellationToken)token;

		try
		{
			while (IsRunning)
			{
				var input = await _reader.ReadLineAsync(realToken);
				OnInputReceived(input);
			}
		}
		catch (TaskCanceledException) { }
	}

	/// <summary>
	/// Stops the reader
	/// </summary>
	public void Stop ()
	{
		if (!IsRunning)
		{
			return;
		}

		_cts.Cancel();
		IsRunning = false;
	}

	/// <summary>
	/// Releases all resources used by the current instance of the <see cref="ConsoleStreamReader"/> class
	/// </summary>
	public void Dispose ()
	{
		if (IsDisposed)
		{
			return;
		}

		IsDisposed = true;
		_reader?.Dispose();
		_cts?.Dispose();
	}

	#endregion

	#region Public - Constructor

	/// <summary>
	/// Initializes a new instance of the <see cref="ConsoleStreamReader"/> class
	/// and starts the reader
	/// </summary>
	public ConsoleStreamReader ()
	{
		_reader = new StreamReader(Console.OpenStandardInput(), Console.InputEncoding);
	}

	#endregion

	#region Private - Properties & Fields

	/// <summary>
	/// Returns true if this object has been disposed of, otherwise false
	/// </summary>
	bool IsDisposed { get; set; }

	#endregion

	#region Private - Methods

	/// <summary>
	/// Invoked when input is read from the console
	/// </summary>
	/// <param name="arg">The input from the console</param>
	void OnInputReceived (string? arg)
	{
		InputReceived?.Invoke(arg);
	}

	#endregion

	#region Private - Static, Constant & Readonly Fields

	readonly StreamReader _reader;
	CancellationTokenSource _cts = new();

	#endregion
}