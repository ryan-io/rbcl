using rbcl.naive;

namespace rbcl.console;

/// <summary>
/// An exercise from Deep DOTNET to understand more of the low level implementation for
/// using async-await
/// This is not code to be used in production and is purely for research purposes
/// </summary>
internal class AsyncTaskThreadExample : IDisposable {
	/// <summary>
	/// This method highlights the issue async/await is trying to solve
	/// Creates 100 work items and queues them all on the thread pool
	/// </summary>
	public void RunQueueWorkerThreadNonDeterministic () {
		int maxConcurrent, maxIOThreds;

		ThreadPool.GetMaxThreads(out maxConcurrent, out maxIOThreds);
		Console.WriteLine($"Max concurrent threads: {maxConcurrent}");
		Console.WriteLine($"Max I/O threads: {maxIOThreds}");

		// we can also use 'AsyncLocal' to define a 'capture clause' for 'i'
		// in the custom implementation, we do not tracked context
		// in real async-await, context is tracked via 'ExecutionContext'
		// without any context tracking in place, using 'AsyncLocal' will always be '0'

		AsyncLocal<int> capture = new();

		for (int i = 0; i < 100; i++) {
			//	var capture = i;
			capture.Value = i;
			//var capture = i;

			// queues work for execution when a thread pool thread becomes available
			//ThreadPool.QueueUserWorkItem(delegate {
			//	// do work
			//	Console.WriteLine(capture);
			//  Thread.Sleep(1000);
			//});

			NaiveThreadPool.QueueUserWorkItem(delegate {
				Console.WriteLine(capture.Value);
				Thread.Sleep(1000);
			});
		}
	}

	public void Dispose () {
		NaiveThreadPool.Dispose();
	}
}