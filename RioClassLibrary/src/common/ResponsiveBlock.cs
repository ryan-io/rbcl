namespace rbcl
{
	//TODO: why the need for a thread?
	public class ResponsiveBlock
	{
		public async Task Wait (Func<bool>? predicate = default)
		{
			if (_started)
			{
				return;
			}

			predicate ??= () => false;
			_started = true;

			while (_thread.IsAlive && _started && !(predicate.Invoke()))
			{
				await Task.Yield();
			}

			Join();
		}

		public void Join ()
		{
			if (!_started)
			{
				return;
			}

			_started = false;
		}

		private bool _started;

		private readonly Thread _thread = Thread.CurrentThread;
	}
}