namespace RioBenchmarkConsole {
	internal class NaiveAsyncExample {
		/// <summary>
		/// This method highlights the issue async/await is trying to solve
		/// Creates 100 work items and queues them all on the thread pool
		/// </summary>
		public void RunQueueWorkerThreadNonDeterministic () {
			int maxConcurrent, maxIOThreds;

			ThreadPool.GetMaxThreads(out maxConcurrent, out maxIOThreds);
			Console.WriteLine($"Max concurrent threads: {maxConcurrent}");
			Console.WriteLine($"Max I/O threads: {maxIOThreds}");

			for (int i = 0; i < 100; i++) {
				var capture = i;

				// queues work for execution when a thread pool thread becomes available
				ThreadPool.QueueUserWorkItem(delegate {
					// do work
					Console.WriteLine(capture);
				});
			}
		}
	}
}