namespace rbcl;

/// <summary>
/// An interface for shutting down an application
/// The assumption is this method will invoke logic for shutting down the application
/// </summary>
public interface IShutdown {
	void Shutdown ();
}

/// <summary>
/// Augments a class that implements IShutdown to provide an application wide cancellation token
///		for handling asynchronous and multi-threaded shutdown operations
/// </summary>
public interface IShutdownSource {
	CancellationToken Token { get; }
}

/// <summary>
/// Default concrete implementation of IShutdownSource & IShutdown
/// </summary>
public class ShutdownSource : IShutdownSource, IShutdown, IDisposable {
	public event Action? OnIosShutdown;
	public event Action? OnLinuxShutdown;
	public event Action? OnWindowsShutdown;
	public event Action? OnMacShutdown;
	public event Action? OnAndroidShutdown;

	/// <summary>
	/// Depends on 'OperatingSystem' and queries the singleton for what the current OS is
	/// Logic specific to a particular OS can be injected
	/// This method will also cancel the internal 'CancellationTokenSource'
	/// </summary>
	/// <exception cref="NotSupportedException">Thrown when the OS is not supported</exception>
	public void Shutdown () {
		if (IsDisposed) {
			return;
		}

		if (OperatingSystem.IsAndroid()) {
			OnAndroidShutdown?.Invoke();
		}
		else if (OperatingSystem.IsIOS()) {
			OnIosShutdown?.Invoke();
		}
		else if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacOS()) {
			OnMacShutdown?.Invoke();
		}
		else if (OperatingSystem.IsLinux()) {
			OnLinuxShutdown?.Invoke();
		}
		else if (OperatingSystem.IsWindows()) {
			OnWindowsShutdown?.Invoke();
		}
		else {
			throw new NotSupportedException(NotSupportErrorMsg);
		}

		Dispose();
	}

	public void Dispose () {
		if (IsDisposed) {
			return;
		}

		_cts.Cancel();
		_cts.Dispose();
		IsDisposed = true;
	}

	public CancellationToken Token => _cts.Token;

	private bool IsDisposed { get; set; }

	readonly CancellationTokenSource _cts = new();

	private const string NotSupportErrorMsg = "Unsupported OS";
}