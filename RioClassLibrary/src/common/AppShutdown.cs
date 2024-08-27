using System.Runtime.InteropServices;

namespace rbcl;

/// <summary>
/// An interface for shutting down an application
/// The assumption is this method will invoke logic for shutting down the application
/// </summary>
public interface IShutdown {
	void Shutdown ();
	bool IsDisposed { get; }
}

/// <summary>
/// Augments a class that implements IShutdown to provide an application wide cancellation token
///		for handling asynchronous and multi-threaded shutdown operations
/// </summary>
public interface IShutdownSource {
	event Action? OnIosShutdown;
	event Action? OnLinuxShutdown;
	event Action? OnWindowsShutdown;
	event Action? OnMacShutdown;
	event Action? OnAndroidShutdown;
	bool IsDisposed { get; }
	CancellationToken Token { get; }
}

/// <summary>
/// Abstraction for determining the current operating system
/// </summary>
public interface IOperatingSystem {
	bool IsAndroid ();
	bool IsIOS ();
	bool IsMacOS ();
	bool IsLinux ();
	bool IsWindows ();
}

/// <summary>
/// A default implementation of IOperatingSystem
/// </summary>
public class OperatingSystemInternal : IOperatingSystem {
	public bool IsAndroid () => OperatingSystem.IsAndroid();                            // Android
	public bool IsIOS () => OperatingSystem.IsIOS();                                    // IOS
	public bool IsMacOS () => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);          // Max
	public bool IsLinux () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);        // Unix
	public bool IsWindows () => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);    // Windows
}

/// <summary>
/// Default concrete implementation of IShutdownSource & IShutdown
/// Use the various events to inject logic for shutting down the application
/// It is up to the consumer to subscribe and unsubscribe from the events as needed
/// The available events are:
///		OnIosShutdown
///		OnLinuxShutdown
///		OnWindowsShutdown
///		OnMacShutdown
///		OnAndroidShutdown
/// </summary>
public class ShutdownSource : IShutdownSource, IShutdown, IDisposable {
	public event Action? OnIosShutdown;
	public event Action? OnLinuxShutdown;
	public event Action? OnWindowsShutdown;
	public event Action? OnMacShutdown;
	public event Action? OnAndroidShutdown;

	public bool IsDisposed { get; private set; }

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

		if (_os.IsAndroid()) {
			OnAndroidShutdown?.Invoke();
		}
		else if (_os.IsIOS()) {
			OnIosShutdown?.Invoke();
		}
		else if (_os.IsMacOS()) {
			OnMacShutdown?.Invoke();
		}
		else if (_os.IsLinux()) {
			OnLinuxShutdown?.Invoke();
		}
		else if (_os.IsWindows()) {
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

	public ShutdownSource (IOperatingSystem os) {
		_os = os;
	}

	private readonly CancellationTokenSource _cts = new();

	private readonly IOperatingSystem _os;

	private const string NotSupportErrorMsg = "Unsupported OS";
}