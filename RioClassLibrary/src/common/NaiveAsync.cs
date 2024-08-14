using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace rbcl;

/// <summary>
/// A simple version of 'Task' that layers on top of 'NaiveThreadPool'
/// Task is really just a data structure in memory for abstracting work performed
///		on a thread pool
/// 46:28
/// </summary>
public class NaiveTask {
	private bool _isComplete;
	private Exception? _exception;
	private Action? _continuation;
	private ExecutionContext? _context;
	private readonly object _lock = new();

	/// <summary>
	/// Queues a work item and completes the operation when the delegate has been invoked
	/// This is quite similar to the base class library of 'Task.Run' except for some
	/// perf optimizations
	/// </summary>
	/// <param name="work"></param>
	/// <returns></returns>
	public static NaiveTask Run (Action work) {
		NaiveTask task = new();

		// do the operation and complete the task
		NaiveThreadPool.QueueUserWorkItem(() => {
			try {
				work();
			}
			catch (Exception e) {
				task.SetException(e);
				return;
			}

			task.SetResult();
		});

		return task;
	}

	#region TASKCOMPLETIONSOURCE

	// the following property and two methods within this region are abstract to a System class called
	// 'TaskCompletionSource'. this is to prevent the consumer from arbitrarily completing the task.
	// For learning and demonstration purposes, I will keep this functionality within the 'NaiveTask' class

	public bool IsCompleted {
		get {
			// need some synchronization here; thread safety
			// the real 'Task' is more robust
			lock (_lock) {
				return _isComplete;
			}
		}
	}

	public void SetResult () {
		Complete(default);
	}

	public void SetException (Exception e) {
		Complete(e);
	}

	#endregion

	/// <summary>
	/// Synchronously wait for work to complete
	/// </summary>
	public void Wait () {
		// a thin wrapper around the kernel's blocking primitive
		ManualResetEventSlim? reset = default;

		lock (_lock) {
			// if the task is already completed, then there is nothing for us to wait for
			if (!_isComplete) {
				reset = new();
				// 'ManualResetEventSlim.Set' allows one or more threads waiting to proceed
				ContinueWith(reset.Set);
			}
		}

		// this blocks the current thread the 'NaiveTask' is running on until we manually unblock it
		// 'Wait' will only run if 'ManualResetEventSlim.Set' is invoked beforehand
		reset?.Wait();

		if (_exception != null) {
			// Watson bucket contains aggregatable information about where the root exception began
			// this information is lost when rethrowing the exception
			//throw _exception;

			// to circumvent this, wrap '_exception' in another 'Exception' or 'AggregateException' (inner exception)
			//throw new AggregateException("", _exception);

			// we can also use something a bit more low level in order to simply append the previous 
			// stacktrace to a 'new' exception; augments the original exception
			// 'ExceptionDispatchInfo.Throw' maintains the original Watson bucket
			ExceptionDispatchInfo.Throw(_exception);
		}
	}

	/// <summary>
	/// We can invoke this method at any point within the synchronous 'Wait' in order to
	/// invoke a callback when the task is complete
	/// </summary>
	public void ContinueWith (Action action) {
		lock (_lock) {
			if (_isComplete) {
				// if task is already done, run this now by queuing work on 'NaiveThreadPool'
				NaiveThreadPool.QueueUserWorkItem(action);
			}
			else {
				// otherwise, cache the continuation and continue as is
				// 'ExecutionContext' is cached for when this task completes so continuation
				//		can be invoked on completion
				_continuation = action;
				_context = ExecutionContext.Capture();
			}
		}
	}

	void Complete (Exception? e) {
		lock (_lock) {
			if (_isComplete) {
				throw new InvalidOperationException("Task is completed.");
			}

			_isComplete = true;
			_exception = e;

			if (_continuation != null) {
				// queue work item that invokes the continuation
				NaiveThreadPool.QueueUserWorkItem(delegate {
					if (_context == null) {
						_continuation();
					}
					else {
						ExecutionContext.Run(
							_context,
							state => ((Action)state!).Invoke(),
							_continuation);
					}
				});
			}
		}
	}
}

/// <summary>
/// An exercise implementing a very simple thread pool analogous to System.Threading.ThreadPool
/// The private constructor creates new 'Thread' objects based on the total number of processors
/// on the consumer's computer. A 'BlockingCollection' is then used to queue up the work
/// passed to the thread pool. This class also works with an analogous 'Task' class, 'NaiveTask'.
/// </summary>
public static class NaiveThreadPool {
	/// <summary>
	/// Adds a work item to a 'BlockingCollection' that will be queued to run   on a thread managed by a thread pool
	/// </summary>
	/// <param name="action">The work to queue on a thread managed by a thread pool</param>
	public static void QueueUserWorkItem (Action action) {
		// 'Action' is chosen as a managed function pointer
		// 'ExecutionContext.Capture' takes a snapshot of the current thread state
		s_workItems.Add((action, ExecutionContext.Capture()));
	}

	/// <summary>
	/// Adds a work item to a 'BlockingCollection' that will be queued to run on a thread managed by a thread pool
	/// This is an unsafe method
	/// Takes an unmanaged pointer to function returning void and no parameters
	/// </summary>
	/// <param name="action"></param>
	public static unsafe void QueueUserWorkItem (delegate*<void> action) {
		// 'ExecutionContext.Capture' takes a snapshot of the current thread state
		s_workItems.Add((() => action(), ExecutionContext.Capture()));    // allocation
	}

	// threads in thread pool will try and take items from this collection in order to
	//		process them
	// if there are no items, the thread pool threads will simply wait until there is work
	//		available
	// 'BlockingCollection' is somewhat analogous to an array, but is thread safe
	static readonly BlockingCollection<(Action, ExecutionContext?)> s_workItems = [];
	// tuple <Action, ExecutionContext?>

	/// <summary>
	/// Creates an appropriate number of threads to run in the background
	/// All threads will stop when 'main()' exits scope
	/// ExecutionContext blog from Stephen Toub
	///		https://devblogs.microsoft.com/pfxteam/executioncontext-vs-synchronizationcontext/
	/// </summary>
	static NaiveThreadPool () {
		int count = 0; // tracking processors
					   // .NET has foreground and background threads
					   // when main() exits, should the threads wait around to finish or stop?
					   // foreground - waits
					   // background - does not wait
					   // the biggest difference in actual implementation is non-fixed thread count
					   //	there is a lot of logic handling the management of threads (increasing, decreasing)
		for (int i = 0; i < Environment.ProcessorCount; i++) {
			count++;
			// 'IsBackground = true' ensures the threads don't keep the process alive after the application closes
			// this prevents them from staying alive forever

			// One key thing to note in regard to ExecutionContext and how threading works:
			//		The intent of ExecutionContext is to solve the problem where a task (or work)
			//		is started on one thread, but finishes on another. When this happens, the thread
			//		where the work is to finish requires information pertaining to the state of the work
			//		when it left its inception thread. ExecutionContext is what provides this state to
			//		the finishing thread.
			// Per Stephen Toub:
			/*
			 * "There is, however, typically a logical flow of control, and we want this ambient data to flow with that control flow, such that the ambient data moves from one thread to another.  This is what ExecutionContext enables."
			 */
			new Thread(() => {
				while (true) {
					// this actually runs our work
					// 'ExecutionContext' transfers state whenever a thread is transferred
					// this occurs during transfers made by 'Thread.Start'
					// https://learn.microsoft.com/en-us/dotnet/api/system.threading.executioncontext?view=net-8.0
					(Action action, ExecutionContext? ctx) = s_workItems.Take();

					/*
					 * When you call the public ExecutionContext.Capture() method, that checks for a current SynchronizationContext, and if there is one, it stores that into the returned ExecutionContext instance.  Then, when the public ExecutionContext.Run method is used, that captured SynchronizationContext is restored as Current during the execution of the supplied delegate.
					 */

					// lines 58-66 track context state and run work appropriately
					if (ctx == null) {
						// run work on whatever thread we are currently on
						action();
					}
					else {
						// run work on the specific thread passed by 'ExecutionContext'
						// 'Run' sets the execution context for the current thread
						ExecutionContext.Run(ctx, state =>
							((Action)state!).Invoke(), action);
					}
				}
			}) { IsBackground = true }.Start();
		}

#if CONSOLE
		Console.WriteLine($"Total threads started: {count}");
#endif
	}

	public static void Dispose () {
		if (IsDisposed) {
			return;
		}

		IsDisposed = true;
		s_workItems.Dispose();
	}

	private static bool IsDisposed { get; set; }
}