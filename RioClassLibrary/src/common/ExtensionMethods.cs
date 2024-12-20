using System.Runtime.CompilerServices;

namespace rbcl {
	public static class ExtensionMethods {
		#region CONVERSIONS

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ToInt (this char c) {
			// ASCII digits start at 48
			return c - '0';
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsDigit (this char c) {
			return c is >= '0' and <= '9'; // 'a'
		}

		#endregion

		#region STRINGS

		public static string Bold (this string str) => "<b>" + str + "</b>";

		public static string Italic (this string str) => "<i>" + str + "</i>";

		public static string Size (this string str, int size) => $"<size={size}>{str}</size>";

		#endregion

		#region UNSAFE & PERFORMANCE

		/// <remarks>Faster analog of Enum.HasFlag</remarks>
		/// <inheritdoc cref="Enum.HasFlag" />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe bool HasFlags<T> (this T first, T second) where T : unmanaged, Enum
			=> HasFlags(&first, &second);


		/// <summary>
		/// Internal helper method for obfuscating logic on checking byte pointers for 'HasFlags'
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveInlining)]
		static unsafe bool HasFlags<T> (T* first, T* second) where T : unmanaged, Enum {
			var pf = (byte*)first;
			var ps = (byte*)second;

			for (var i = 0; i < sizeof(T); i++)
				if ((pf[i] & ps[i]) != ps[i])
					return false;

			return true;
		}

		#endregion

		#region COLLECTIONS

		/// <summary>
		///  https://briancaos.wordpress.com/2022/07/04/c-list-batch-braking-an-ienumerable-into-batches-with-net/
		///  This method will break an IEnumerable into batches of a given size. Any overflow will be returned as a final batch.
		/// </summary>
		/// <param name="enumerator">Collection to batch</param>
		/// <param name="size">Batch size</param>
		/// <typeparam name="T">Generic type</typeparam>
		/// <returns>Enumerable of an enumerable with appropriate batch sizes. The final element may not have equal size
		///  due to it being use as 'overflow'</returns>
		public static IEnumerable<IEnumerable<T>?> Batch<T> (this IEnumerable<T> enumerator, int size) {
			T[]? batch = null;
			var count = 0;

			foreach (var item in enumerator) {
				batch ??= new T[size];

				batch[count++] = item;
				if (count != size)
					continue;

				yield return batch;

				batch = null;
				count = 0;
			}

			if (batch != null && count > 0)
				yield return batch.Take(count).ToArray();
		}

		#endregion
	}
}