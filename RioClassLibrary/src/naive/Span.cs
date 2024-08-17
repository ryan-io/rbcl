using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace rbcl;

/// <summary>
/// A 'naive' implementation of Span based on Stephen Toub's dotnet deep dive video on Span
/// This span implementation is purely for demo purposes and for learning
/// This is a readonly ref struct (can only be allocated on the stack) and cannot escape
///		to managed heap
/// Models contiguous data with mutable creation, immutable instance at a later point in time
/// </summary>
public readonly ref struct Span<T> where T : unmanaged {
	public Span (ref T value) {
		_value = ref value;
		_length = 1;
	}

	/// <summary>
	/// This is the where performance benefits when working with arrays and collections comes from
	/// Takes a reference to an array or collection and gets the stack reference to the first
	///		element in the array or collection and assigns to '_value'
	/// Assigns '_length' as the length of the array or collection
	/// </summary>
	public Span (ref T[] valueArray) {
		ArgumentNullException.ThrowIfNull(valueArray);

		// so we do not have to pass in the length of the array
		// return type is of type ref
		// I like to think of 'MemoryMarshal.GetArrayDataReference' analogously like pointer decay in C++
		//		the intent is to get a reference to the head element of an array
		_value = ref MemoryMarshal.GetArrayDataReference(valueArray);   //System.Runtime.Interop
		_length = valueArray.Length;
	}

	/// <summary>
	/// A copy constructor that constructs a new 'Span' that points to the same '_value' and
	///		'_length' address as the 'Span' to copy
	/// </summary>
	public Span (Span<T> span) {
		_value = ref span._value;
		_length = span._length;
	}

	/// <summary>
	/// A copy constructor for System.Span that constructs a new 'Span'
	///		that points 'span[0]' for '_value' and 'span.Length' for '_length'
	/// </summary>
	public Span (ref System.Span<T> span) {
		_value = ref span[0];
		_length = span.Length;
	}

	/// <summary>
	/// A constructor for an unsafe context utilizing a head pointer and a length buffer
	/// There are no bounds checking in this method; ensure you have a use case for it
	/// This method is a terrific candidate for dangling pointers due to not managing scope
	/// </summary>
	public unsafe Span (T* ptr, int length) {
		_value = ref *ptr;
		_length = length;
	}

	/// <summary>
	/// Gets a reference to an element of the span
	/// Check boundaries before returning the reference
	/// Throw 'IndexOutOfRangeException' if index is out-of-bounds
	/// Throw 'OverflowException' if sizeof(int) * index is > Int32.Max
	/// </summary>
	public ref T this[int index] {
		get {
			if ((uint)index >= (uint)_length) // this piece of code allows for us to not check if < 0 (uint wraps)
			{
				throw new IndexOutOfRangeException();
			}

			// care should be taken for integer overflow here
			// internally, the amount of memory allocated is the two method parameters multiplied (ref source, element value)
			//		'sizeof(source) * index'; ensure this value < Int32Max
			int.CreateChecked(sizeof(int) * index); // this method throws an overflow exception if the value is > Int32.Max
			return ref Unsafe.Add(ref _value, index);   // if we omit 'ref' keywords, this is a by-value return method
														// 'ref' return allows us to simply write into the value stored at that memory address
		}
	}

	/// <summary>
	/// Returns a new 'Span' starting at the specified offset until the end of contiguous memory
	/// Throws 'ArgumentOutOfRangeException' if offset is outside bounds
	/// </summary>
	/// <param name="offest"></param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public Span<T> Slice (int offest) {
		// this checks if offset is outside contiguous memory bounds
		ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)offest, (uint)_length);

		/*
		 *	if ((uint)offest >= (uint)_length) {
				throw new ArgumentOutOfRangeException();
			}
		 */

		return new Span<T>(ref Unsafe.Add(ref _value, offest), _length - offest);
	}

	/// <summary>
	/// Length of contiguously allocated memory
	/// </summary>
	public int Length => _length;

	/// <summary>
	/// Internal constructor for manually specifying the length of the 'Span'
	/// Thought to eliminate user error by specifying the length manually
	/// If this is required, see the unsafe constructor see 'unsafe Span (T* ptr, int length)'
	/// </summary>
	private Span (ref T reference, int length) {
		_value = ref reference; _length = length;
	}

	private readonly ref T _value;
	private readonly int _length;
}