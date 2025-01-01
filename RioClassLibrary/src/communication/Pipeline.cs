using System.Collections.Concurrent;

namespace rbcl.communication;

#region Pipeline Object

/// <summary>
/// Represents the arguments passed to a command pipeline.
/// This is an abstract class and should be inherited by a concrete implementation.
/// </summary>
public abstract class PipelinePayload<T> : IPayload<T>
{
	/// <inheritdoc/>
	public T Payload { get; }

	/// <summary>
	/// Gets an empty instance of the <see cref="PipelinePayload{T}"/> class.
	/// </summary>
	/// <remarks>
	/// The Empty property represents an empty instance of the <see cref="EmptyPipelinePayload"/> class,
	/// and is often used as a placeholder when a null value is not desired.
	/// </remarks>
	/// <value>
	/// An empty instance of the <see cref="EmptyPipelinePayload"/> class.
	/// </value>
	public static PipelinePayload<object> Empty { get; } = new EmptyPipelinePayload(new object());

	/// <summary>
	/// Gets or sets the current state of the command pipeline.
	/// </summary>
	/// <value>
	/// The current state of the command pipeline.
	/// </value>
	public PipelineState CurrentState { get; internal set; } = PipelineState.None;

	/// <summary>
	/// Protected constructor used to initialize the payload
	/// </summary>
	protected PipelinePayload (T payload) { Payload = payload; }

	/// <summary>
	/// Wrapper class for an empty pipeline object
	/// </summary>
	internal class EmptyPipelinePayload : PipelinePayload<object>
	{
		internal EmptyPipelinePayload (object payload) : base(payload) { }
	}
}

#endregion

/// <summary>
/// Base class for a pipeline error
/// </summary>
public abstract class PipelineError : Exception { }

#region Pipeline

/// <summary>
/// Represents a command pipeline that executes a series of asynchronous pipeline delegates.
/// </summary>
public abstract class Pipeline<T> : IPipeline<T>
{
	#region Public Events

	/// <summary>
	/// Synchronous event that is raised when the pipeline starts.
	/// This is invoked prior to the start of asynchronous work
	/// </summary>
	public event PipelineDelegate? Started;

	/// <summary>
	/// Synchronous event that is raised when the pipeline work begins.
	/// This is invoked prior to the start of asynchronous work
	/// </summary>
	public event PipelineDelegate? Working;

	/// <summary>
	/// Synchronous event that is raised when the pipeline ends.
	/// This is invoked after the asynchronous work has completed
	/// </summary>
	public event PipelineDelegate? Ended;

	/// <summary>
	/// Synchronous event that is raised when an error is caught in the pipeline.
	/// This is invoked when an exception is thrown during the pipeline execution, prior to asynchronous work
	/// </summary>
	public event PipelineErrorDelegate? ErrorCaught;

	#endregion

	#region Public Delegates

	/// <summary>
	/// Represents a delegate for an asynchronous command pipeline.
	/// </summary>
	/// <param name="sender">The original sender throughout the call stack</param>
	/// <param name="payload">The command pipeline arguments.</param>
	/// <param name="token">Optional cancellation token to cancel the pipeline execution.</param>
	/// <returns>A task representing the asynchronous pipeline execution.</returns>
	public delegate Task PipelineWorkTaskDelegate (object? sender, PipelinePayload<T> payload, CancellationToken? token = default);

	/// <summary>
	/// Represents a delegate when the pipeline throws an exception
	/// </summary>
	/// <param name="sender">The original sender throughout the call stack</param>
	/// <param name="payload">The command pipeline arguments.</param>
	/// <param name="error">The exception that was thrown.</param>
	/// <param name="token">Optional cancellation token to cancel the pipeline execution.</param>
	/// <returns>A task representing the asynchronous pipeline execution.</returns>
	public delegate Task PipelineErrorTaskDelegate (object? sender, PipelinePayload<T> payload, PipelineError error, CancellationToken? token = default);

	/// <summary>
	/// Represents a synchronous delegate for various state changes within the command pipeline.
	/// </summary>
	/// <param name="sender">The original sender throughout the call stack</param>
	/// <param name="payload">The command pipeline arguments.</param>
	public delegate void PipelineDelegate (object? sender, PipelinePayload<T> payload);

	/// <summary>
	/// Represents a synchronous delegate for various state changes within the command pipeline.
	/// </summary>
	/// <param name="sender">The original sender throughout the call stack</param>
	/// <param name="payload">The command pipeline arguments.</param>
	/// <param name="error">Error that was caught.</param>
	public delegate void PipelineErrorDelegate (object? sender, PipelinePayload<T> payload, PipelineError error);

	#endregion

	#region Public Properties

	public PipelineState State { get; private set; } = PipelineState.None;

	#endregion

	#region Public Methods

	public async Task SignalAsync (PipelinePayload<T> payload, TimeSpan timeout = default, CancellationToken? token = default)
	{
		if (State is PipelineState.None or not PipelineState.Idle)
		{
			return;
		}

		if (Monitor.TryEnter(_mutex, timeout))
		{
			try
			{
				await _cts.CancelAsync();
				_cts.Dispose();

				if (token != null && token != CancellationToken.None)
				{
					_cts = CancellationTokenSource.CreateLinkedTokenSource(token.Value);
				}
				else
				{
					_cts = new CancellationTokenSource();
				}

				// state -> start
				State = PipelineState.Start;
				Started?.Invoke(this, payload);
				await OnStart(payload, _cts.Token);

				// state -> working
				State = PipelineState.Working;
				Working?.Invoke(this, payload);
				await OnWork(payload, _cts.Token);

				// state -> end
				State = PipelineState.End;
				Ended?.Invoke(this, payload);
				await OnEnd(payload, _cts.Token);
			}
			catch (PipelineError error)
			{
				State = PipelineState.Error;
				ErrorCaught?.Invoke(this, payload, error);
				await OnError(payload, error, _cts.Token);
			}
			finally
			{
				// state -> end
				State = PipelineState.End;
				Ended?.Invoke(this, payload);
				await OnEnd(payload, _cts.Token);

				//state -> idle
				State = PipelineState.Idle;

				Monitor.Exit(_mutex);
			}
		}
		else
		{
			// logic for if the mutex is locked 
			// this implies that the pipeline is currently running
		}
	}

	#region Work Registration

	/// <inheritdoc/>
	public IPipeline<T> QueueWorkItem (PipelineWorkTaskDelegate workItem)
	{
		_workItems.TryAdd(workItem.GetHashCode(), workItem);
		return this;
	}

	/// <inheritdoc/>
	public IPipeline<T> DeqeueWorkItem (PipelineWorkTaskDelegate workItem)
	{
		_workItems.TryRemove(workItem.GetHashCode(), out _);
		return this;
	}

	/// <inheritdoc/>
	public IPipeline<T> QueueStartItem (PipelineWorkTaskDelegate startItem)
	{
		_startItems.TryAdd(startItem.GetHashCode(), startItem);
		return this;
	}

	/// <inheritdoc/>
	public IPipeline<T> DequeueStartItem (PipelineWorkTaskDelegate startItem)
	{
		_startItems.TryRemove(startItem.GetHashCode(), out _);
		return this;
	}

	/// <inheritdoc/>
	public IPipeline<T> QueueEndItem (PipelineWorkTaskDelegate endItem)
	{
		_startItems.TryAdd(endItem.GetHashCode(), endItem);
		return this;
	}

	/// <inheritdoc/>
	public IPipeline<T> DequeueEndItem (PipelineWorkTaskDelegate endItem)
	{
		_endItems.TryRemove(endItem.GetHashCode(), out _);
		return this;
	}

	/// <inheritdoc/>
	public IPipeline<T> RegisterOnError (PipelineErrorTaskDelegate errorItem)
	{
		_errorItems.TryAdd(errorItem.GetHashCode(), errorItem);
		return this;
	}

	/// <inheritdoc/>
	public IPipeline<T> UnregisterOnError (PipelineErrorTaskDelegate errorItem)
	{
		_errorItems.TryRemove(errorItem.GetHashCode(), out _);
		return this;
	}

	#endregion

	#endregion

	#region Constructor

	/// <summary>
	/// Default constructor
	/// </summary>
	public Pipeline () { }

	#endregion

	#region Private Methods

	/// <summary>
	/// Beings pipeline start operations
	/// </summary>
	async Task OnStart (PipelinePayload<T> payload, CancellationToken? token)
	{
		if (_startItems.Count > 0)
		{
			await OptimizeWork(_startItems, payload, token);
		}
	}

	/// <summary>
	/// Beings pipeline work operations
	/// </summary>
	async Task OnWork (PipelinePayload<T> payload, CancellationToken? token)
	{
		if (_workItems.Count > 0)
		{
			await OptimizeWork(_workItems, payload, token);
		}
	}

	/// <summary>
	/// Beings pipeline end operations
	/// </summary>
	async Task OnEnd (PipelinePayload<T> payload, CancellationToken? token)
	{
		if (_endItems.Count > 0)
		{
			await OptimizeWork(_endItems, payload, token);
		}
	}

	/// <summary>
	/// Beings pipeline error operations
	/// </summary>
	async Task OnError (PipelinePayload<T> payload, PipelineError error, CancellationToken? token)
	{
		if (_errorItems.Count > 0)
		{
			await OptimizeError(_errorItems, payload, error, token);
		}
	}

	/// <summary>
	/// Optimizes the work by using parallelism when the number of work items exceeds a certain threshold.
	/// </summary>
	/// <param name="col">The concurrent collection to work with</param>
	/// <param name="payload">The pipeline payload</param>
	/// <param name="token">Optional cancellation token</param>
	async Task OptimizeWork (ConcurrentDictionary<int, PipelineWorkTaskDelegate> col, PipelinePayload<T> payload, CancellationToken? token)
	{
		if (col.Count > PerfToggle)
		{
			var options = new ParallelOptions()
			{
				MaxDegreeOfParallelism = -1 // -1 when used in conjecture with Parallel.ForEachAsync uses ProcessorCount
			};

			await Parallel.ForEachAsync(col, options, async (item, _) =>
			{
				await item.Value.Invoke(this, payload, token);
			});
		}
		else
		{
			await Task.WhenAll(col.Values.Select(b => b.Invoke(this, payload, token)));
		}
	}

	/// <summary>
	/// Optimizes the work by using parallelism when the number of work items exceeds a certain threshold.
	/// </summary>
	/// <param name="col">The concurrent collection to work with</param>
	/// <param name="payload">The pipeline payload</param>
	/// <param name="error"></param>
	/// <param name="token">Optional cancellation token</param>
	async Task OptimizeError (ConcurrentDictionary<int, PipelineErrorTaskDelegate> col, PipelinePayload<T> payload, PipelineError error, CancellationToken? token)
	{
		if (col.Count > PerfToggle)
		{
			var options = new ParallelOptions()
			{
				MaxDegreeOfParallelism = -1 // -1 when used in conjecture with Parallel.ForEachAsync uses ProcessorCount
											// MaxDegreeOfParallelism = Environment.ProcessorCount
			};

			await Parallel.ForEachAsync(col, options, async (item, _) =>
			{
				await item.Value.Invoke(this, payload, error, token);
			});
		}
		else
		{
			await Task.WhenAll(col.Values.Select(b => b.Invoke(this, payload, error, token)));
		}
	}

	#endregion

	#region Private, Readonly, & Constant Fields

	private CancellationTokenSource _cts = new();

	/*
		This following collections are of type 'ConConcurrentDictionary' to ensure atomic operations
		when adding and removing from them.
		Operations are thread safe, but may not be atomic.
	 */
	private readonly ConcurrentDictionary<int, PipelineWorkTaskDelegate> _workItems = [];
	private readonly ConcurrentDictionary<int, PipelineWorkTaskDelegate> _startItems = [];
	private readonly ConcurrentDictionary<int, PipelineWorkTaskDelegate> _endItems = [];
	private readonly ConcurrentDictionary<int, PipelineErrorTaskDelegate> _errorItems = [];

	private readonly object _mutex = new();
	private const int PerfToggle = 16;

	#endregion
}

#endregion

#region Contracts

/// <summary>
/// Contract to expose a public get-only property of type T signifying a data payload.
/// </summary>
public interface IPayload<out T>
{
	/// <summary>
	/// Payload object to pass through the pipeline
	/// </summary>
	T Payload { get; }

	/// <summary>
	/// Current state of the pipeline payload
	/// </summary>
	PipelineState CurrentState { get; }
}

/// <summary>
/// Represents a complete pipeline object
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IPipeline<T> : IPipelineSignal<T>, IPipelineWorkRegister<T>, IPipelineErrorRegister<T>
{
	/// <summary>
	/// The current state of the pipeline
	/// </summary>
	PipelineState State { get; }
}

/// <summary>
/// Signals the pipeline to start
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IPipelineSignal<T>
{
	/// <summary>
	/// Signals the pipeline to begin
	/// </summary>
	/// <param name="payload">The <see cref="PipelinePayload{T}"/> object containing the event data. </param>
	/// <param name="timeout">Time in milliseconds before the try-start method returns. Default is to asynchronously wait (timeout = -1)</param>
	/// <param name="token">A <see cref="CancellationToken"/> to cancel the operation. If not specified, <see cref="CancellationToken.None"/> is used.</param>
	/// <returns>A <see cref="Task"/> representing a unit of work</returns>
	Task SignalAsync (PipelinePayload<T> payload, TimeSpan timeout = default, CancellationToken? token = default);
}

/// <summary>
/// API for registering and unregistering work to and from the pipeline
/// </summary>
public interface IPipelineWorkRegister<T>
{
	/// <summary>
	/// Queues a work item to the pipeline.
	/// </summary>
	/// <param name="workItem">Work item to queue</param>
	IPipeline<T> QueueWorkItem (Pipeline<T>.PipelineWorkTaskDelegate workItem);

	/// <summary>
	/// Dequeues a work item to the pipeline.
	/// </summary>
	/// <param name="workItem">Work item to dequeue</param>
	IPipeline<T> DeqeueWorkItem (Pipeline<T>.PipelineWorkTaskDelegate workItem);

	/// <summary>
	/// Queues a start item to the pipeline.
	/// </summary>
	/// <param name="startItem">Start item to queue</param>
	IPipeline<T> QueueStartItem (Pipeline<T>.PipelineWorkTaskDelegate startItem);

	/// <summary>
	/// Dequeues a start item to the pipeline.
	/// </summary>
	/// <param name="startItem">Start item to dequeue</param>
	IPipeline<T> DequeueStartItem (Pipeline<T>.PipelineWorkTaskDelegate startItem);

	/// <summary>
	/// Queues an end item to the pipeline.
	/// </summary>
	/// <param name="endItem">End item to dequeue</param>
	IPipeline<T> QueueEndItem (Pipeline<T>.PipelineWorkTaskDelegate endItem);

	/// <summary>
	/// Dequeues an end item to the pipeline.
	/// </summary>
	/// <param name="endItem">End item to dequeue</param>
	IPipeline<T> DequeueEndItem (Pipeline<T>.PipelineWorkTaskDelegate endItem);
}

/// <summary>
/// API for handling errors caught 
/// </summary>
public interface IPipelineErrorRegister<T>
{
	/// <summary>
	/// Queues a work item to the error branch of the pipeline.
	/// </summary>
	/// <param name="errorItem">Work item to queue when an error is caught</param>
	IPipeline<T> RegisterOnError (Pipeline<T>.PipelineErrorTaskDelegate errorItem);

	/// <summary>
	/// Dequeues a work item to the error branch of the pipeline.
	/// </summary>
	/// <param name="errorItem">Work item to queue when an error is caught</param>
	IPipeline<T> UnregisterOnError (Pipeline<T>.PipelineErrorTaskDelegate errorItem);
}

#endregion

#region Enums

/// <summary>
/// Represents the different states of a command pipeline.
/// </summary>
public enum PipelineState
{
	None,
	Idle,
	Start,
	Working,
	End,
	Error,
}

#endregion

#region Internal

/// <summary>
/// Contains extension methods for internal use.
/// </summary>
internal static class InternalExtensions
{
	/// <summary>
	/// Filters an array of delegates to return a list containing only delegates of a specific type.
	/// </summary>
	/// <typeparam name="T">The type of delegate to filter for.</typeparam>
	/// <param name="delegates">The array of delegates to filter.</param>
	/// <returns>A list of delegates of the specified type.</returns>
	internal static List<T> DelegatesAs<T> (this Delegate[] delegates) where T : Delegate
	{
		var output = new List<T>();

		foreach (var del in delegates)
		{
			if (del is not T asTypeDelegate)
				continue;

			output.Add(asTypeDelegate);
		}

		return output;
	}
}

#endregion