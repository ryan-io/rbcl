using FluentAssertions;
using NSubstitute;

namespace rbcl.tests.unit {
	public class ShutdownSourceTests {
		private readonly IOperatingSystem _os = Substitute.For<IOperatingSystem>();
		private readonly ShutdownSource _sut;

		public ShutdownSourceTests () {
			_sut = new ShutdownSource(_os);
		}

		[Fact]
		public void Shutdown_ShouldInvokeOnAndroidShutdown_WhenOSIsAndroid () {
			// Arrange
			_os.IsAndroid().Returns(true);
			_os.IsLinux().Returns(false);
			_os.IsMacOS().Returns(false);
			_os.IsWindows().Returns(false);
			_os.IsIOS().Returns(false);

			using var monitor = _sut.Monitor();

			// Act
			_sut.Shutdown();

			// Assert
			monitor.Should().Raise(nameof(ShutdownSource.OnAndroidShutdown));
		}

		[Fact]
		public void Shutdown_ShouldInvokeOnIosShutdown_WhenOSIsIOS () {
			// Arrange
			_os.IsAndroid().Returns(false);
			_os.IsLinux().Returns(false);
			_os.IsMacOS().Returns(false);
			_os.IsWindows().Returns(false);
			_os.IsIOS().Returns(true);

			using var monitor = _sut.Monitor();

			// Act
			_sut.Shutdown();

			// Assert
			monitor.Should().Raise(nameof(ShutdownSource.OnIosShutdown));
		}

		[Fact]
		public void Shutdown_ShouldInvokeOnMacShutdown_WhenOSIsMacOS () {
			// Arrange
			_os.IsAndroid().Returns(false);
			_os.IsLinux().Returns(false);
			_os.IsMacOS().Returns(true);
			_os.IsWindows().Returns(false);
			_os.IsIOS().Returns(false);

			using var monitor = _sut.Monitor();

			// Act
			_sut.Shutdown();

			// Assert
			monitor.Should().Raise(nameof(ShutdownSource.OnMacShutdown));
		}

		[Fact]
		public void Shutdown_ShouldInvokeOnLinuxShutdown_WhenOSIsLinux () {
			// Arrange
			_os.IsAndroid().Returns(false);
			_os.IsLinux().Returns(true);
			_os.IsMacOS().Returns(false);
			_os.IsWindows().Returns(false);
			_os.IsIOS().Returns(false);

			using var monitor = _sut.Monitor();

			// Act
			_sut.Shutdown();

			// Assert
			monitor.Should().Raise(nameof(ShutdownSource.OnLinuxShutdown));
		}

		[Fact]
		public void Shutdown_ShouldInvokeOnWindowsShutdown_WhenOSIsWindows () {
			// Arrange
			_os.IsAndroid().Returns(false);
			_os.IsLinux().Returns(false);
			_os.IsMacOS().Returns(false);
			_os.IsWindows().Returns(true);
			_os.IsIOS().Returns(false);

			using var monitor = _sut.Monitor();

			// Act
			_sut.Shutdown();

			// Assert
			monitor.Should().Raise(nameof(ShutdownSource.OnWindowsShutdown));
		}

		[Fact]
		public void Shutdown_ShouldThrowNotSupportedException_WhenOSIsNotSupported () {
			// Arrange
			_os.IsAndroid().Returns(false);
			_os.IsLinux().Returns(false);
			_os.IsMacOS().Returns(false);
			_os.IsWindows().Returns(false);
			_os.IsIOS().Returns(false);

			// Act
			Action throwExceptionAction = () => _sut.Shutdown();

			// Assert
			throwExceptionAction.Should().Throw<NotSupportedException>();
		}

		[Fact]
		public void Dispose_ShouldCancelAndDisposeTokenSource_WhenCalled () {
			// Act
			_sut.Dispose();

			// Assert
			_sut.IsDisposed.Should().BeTrue();
		}

		[Fact]
		public void Token_ShouldReturnCancellationToken () {
			// Act
			var token = _sut.Token;

			// Assert
			token.Should().NotBeNull();
		}
	}
}
