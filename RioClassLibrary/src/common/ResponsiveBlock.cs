namespace rbcl
{
	/// <summary>
	/// Asynchronous block that can be waited on
	/// </summary>
	public class ResponsiveBlock
	{
		/// <summary>
		/// This method will wait until the predicate returns true
		/// </summary>
		/// <param name="predicate">Criteria for when the block should suspend, should return true you want to stop a responsive block</param>
		/// <returns>Awaitable task</returns>
		public async Task Wait (Func<bool>? predicate = default)
		{
			if (_started)
			{
				return;
			}

			predicate ??= () => true;
			_started = true;

			while (_started && predicate.Invoke())
			{
				await _task;
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
		private readonly Task _task = Task.Delay(TimeSpan.FromMilliseconds(1));
	}
}