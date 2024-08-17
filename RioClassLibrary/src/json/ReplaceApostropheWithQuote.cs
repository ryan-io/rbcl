namespace rbcl;

/// <summary>
/// Stack allocates a span from string 'str' and replaces /' with /"
/// Returns a new string
/// </summary>
public ref struct ReplaceApostropheWithQuote {
	public string Modify (ref string str) {
		// allocate the total number of chars in 'str' on the stack
		System.Span<char> span = stackalloc char[str.Length];
		str.CopyTo(span);
		span.Replace('\'', '\"');
		return span.ToString();
	}
}