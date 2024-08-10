namespace rbcl;
/// <summary>
/// For use when a reference is required within an asynchronous context
/// This class acts as a wrapper to a specific unit of work of type 'T'
/// </summary>
public class AsyncRef<T> {
	public AsyncRef () {
	}

	public AsyncRef (T value) => Value = value;

	public T? Value { get; set; }

	public override string? ToString () {
		var value = Value;
		return value == null ? "" : value.ToString();
	}

	public static implicit operator T? (AsyncRef<T?> r) => r.Value;

	public static implicit operator AsyncRef<T> (T value) => new(value);
}
