using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;

namespace rbcl.naive {
	/// <summary>
	/// A naive implementation of an array pool
	/// Worth reviewing Toub/Hanselman's ArrayPool<T> dotnet YouTube video
	/// </summary>
	/// <typeparam name="T">Any type with a default parameterless constructor</typeparam>
	public class NaiveArrayPool<T> where T : new() {
		public static T[] Rent (uint minimumLength) {
			var target = BitOperations.RoundUpToPowerOf2(minimumLength);

			// if the minimum length is less than 1, return an empty array
			ArgumentOutOfRangeException.ThrowIfEqual<uint>(target, 0);

			// if the consumer has not met the critical length, return a new array
			if (target <= CriticalLength) {
				return new T[target];
			}

			var state = s_pool[target].TryDequeue(out var array);

			if (state && array != null) {
				return array;
			}

			return new T[target];
		}

		public static void Return (T[] array) {
			if (array.Length < 1) {
				return;
			}

			// if it is not a power of 2, return
			if (BitOperations.IsPow2(array.Length)) {
				var state = s_pool.TryGetValue((uint)array.Length, out var queue);

				if (state && queue != null)
				{
					queue.Enqueue(array);
				}
				else {
					s_pool.TryAdd((uint)array.Length, new ConcurrentQueue<T[]> { });
				}
			}
		}

		static NaiveArrayPool()
		{
			int tracker = 0;
			int start = 16;

			while (tracker < 29)
			{
				s_pool.TryAdd((uint)start, new ConcurrentQueue<T[]>());
				start *= 2;
				tracker++;
			}
		}

		static uint Po2Index(uint current) => (uint)BitOperations.Log2(current);

		// At a certain length (power of 2), we will not pool the array
		// This critical length is ~16
		// The BCL is of type ArrayPool<T> and has a default max array length of 1024 
		private const int CriticalLength = 16;

		// Backing data structure for maintaining pools of arrays
		private static readonly ConcurrentDictionary<uint, ConcurrentQueue<T[]>> s_pool = new();
	}
}
