namespace rbcl;

/// <summary>
/// Thread safe data structure wrapping an integer that is only allocated on the stack
/// Care for scope must be enacted
/// This is a much more verbose alternative to Interlocked
/// </summary>
public ref struct IntSafe {
	/// <summary>
	///  Thread safe value of type integer.
	/// </summary>
	public int Value {
		get => _value;
		private set => Interlocked.Exchange(ref _value, value);
	}

	/// <summary>
	///  The value that is protected by the mutex. Backing field to Value.
	/// </summary>
	int _value;

	/// <summary>
	///  Increment operator
	/// </summary>
	/// <param name="safe">this* IntSafe</param>
	/// <returns>IntSafe&</returns>
	public static IntSafe operator ++ (IntSafe safe) {
		safe.Value++;
		return safe;
	}

	/// <summary>
	///  Decrement operator
	/// </summary>
	/// <param name="safe">this* IntSafe</param>
	/// <returns>IntSafe&</returns>
	public static IntSafe operator -- (IntSafe safe) {
		safe.Value--;
		return safe;
	}

	/// <summary>
	/// Implicit operator to case from IntSafe to int
	/// </summary>
	/// <returns>IntSafe as int</returns>
	public static implicit operator int (IntSafe intSafe) => intSafe._value;


	/// <summary>
	/// Implicit operator to case from IntSafe to int
	/// </summary>
	/// <returns>int to new IntSafe object (allocation)</returns>
	public static implicit operator IntSafe (int integer) => new(integer);

	/// <summary>
	///  Implicit conversion from IntSafe to string.
	/// </summary>
	/// <returns>string object</returns>
	public override string ToString () => Value.ToString();

	/// <summary>
	///  Constructor that takes an optional start value.
	/// </summary>
	/// <param name="start"></param>
	public IntSafe (int start = 0) {
		_value = 0;
		Value = start;
	}
}