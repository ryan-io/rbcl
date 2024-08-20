using rbcl.console;

var ex = new JsonValidatorExample();
ex.Run();

// for long running operations that will block a thread pool thread in its entirety
Task.Factory.StartNew(async () => {
	while (true) {
		Console.WriteLine($"Running...{Thread.CurrentThread.IsThreadPoolThread}");
		Console.WriteLine($"State: {Thread.CurrentThread.ThreadState}");
		await Task.Delay(TimeSpan.FromSeconds(2));
	}

	return Task.CompletedTask;
}, TaskCreationOptions.LongRunning);

Console.ReadLine();