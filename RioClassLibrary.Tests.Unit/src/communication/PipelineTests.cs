using FluentAssertions;
using NSubstitute;
using rbcl.communication;

namespace rbcl.tests.unit.communication
{
	public class PipelineTests
	{
		private readonly Pipeline<object> _pipeline;
		private readonly IPayload<object> _mockPayload;
		private readonly Pipeline<object>.PipelineWorkTaskDelegate _mockWorkTask;
		private readonly Pipeline<object>.PipelineErrorTaskDelegate _mockErrorTask;

		public PipelineTests ()
		{
			_pipeline = new Pipeline<object>();
			_mockPayload = Substitute.For<IPayload<object>>();
			_mockWorkTask = Substitute.For<Pipeline<object>.PipelineWorkTaskDelegate>();
			_mockErrorTask = Substitute.For<Pipeline<object>.PipelineErrorTaskDelegate>();
		}

		[Fact]
		public async Task SignalAsync_ShouldSignalWorkItems ()
		{
			// Arrange
			_pipeline.QueueWorkItem(_mockWorkTask);

			// Act
			await _pipeline.SignalAsync(_mockPayload);

			// Assert
			await _mockWorkTask.Received(1).Invoke(Arg.Any<object>(), Arg.Any<IPayload<object>>(), Arg.Any<CancellationToken>());
		}

		[Fact]
		public void StopSignal_ShouldStopPipeline ()
		{
			// Act
			var result = _pipeline.StopSignal();

			// Assert
			result.Should().BeTrue();
		}

		[Fact]
		public void QueueWorkItem_ShouldAddWorkItem ()
		{
			// Act
			_pipeline.QueueWorkItem(_mockWorkTask);

			// Assert
			_pipeline.QueueWorkItem(_mockWorkTask).Should().Be(_pipeline);
		}

		[Fact]
		public void DequeueWorkItem_ShouldRemoveWorkItem ()
		{
			// Arrange
			_pipeline.QueueWorkItem(_mockWorkTask);

			// Act
			_pipeline.DeqeueWorkItem(_mockWorkTask);

			// Assert
			_pipeline.DeqeueWorkItem(_mockWorkTask).Should().Be(_pipeline);
		}

		[Fact]
		public void RegisterOnError_ShouldAddErrorTask ()
		{
			// Act
			_pipeline.RegisterOnError(_mockErrorTask);

			// Assert
			_pipeline.RegisterOnError(_mockErrorTask).Should().Be(_pipeline);
		}

		[Fact]
		public void UnregisterOnError_ShouldRemoveErrorTask ()
		{
			// Arrange
			_pipeline.RegisterOnError(_mockErrorTask);

			// Act
			_pipeline.UnregisterOnError(_mockErrorTask);

			// Assert
			_pipeline.UnregisterOnError(_mockErrorTask).Should().Be(_pipeline);
		}

		[Fact]
		public async Task OnStart_ShouldInvokeStartItems ()
		{
			// Arrange
			_pipeline.QueueStartItem(_mockWorkTask);

			// Act
			await _pipeline.SignalAsync(_mockPayload);

			// Assert
			await _mockWorkTask.Received(1).Invoke(Arg.Any<object>(), Arg.Any<IPayload<object>>(), Arg.Any<CancellationToken>());
		}

		[Fact]
		public async Task OnEnd_ShouldInvokeEndItems ()
		{
			// Arrange
			_pipeline.QueueEndItem(_mockWorkTask);

			// Act
			await _pipeline.SignalAsync(_mockPayload);

			// Assert
			await _mockWorkTask.Received(1).Invoke(Arg.Any<object>(), Arg.Any<IPayload<object>>(), Arg.Any<CancellationToken>());
		}

		[Fact]
		public async Task OnError_ShouldInvokeErrorItems ()
		{
			// Arrange
			_pipeline.RegisterOnError(_mockErrorTask);
			_pipeline.QueueWorkItem(async (_, _, token) =>
			{
				token ??= CancellationToken.None;
				await Task.Delay(1000, token.Value);
			});

			// Act
			var task1 = Task.Run(async () => await _pipeline.SignalAsync(_mockPayload));
			var task2 = Task.Run(() => _pipeline.StopSignal());

			await Task.WhenAll(task1, task2);

			// Assert
			await _mockErrorTask.Received(1).Invoke(
				Arg.Any<object>(),
				Arg.Any<IPayload<object>>(),
				Arg.Any<PipelineError>(),
				Arg.Any<CancellationToken>());
		}
	}
}
