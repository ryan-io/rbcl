namespace rbcl;

/// <summary>
/// Stack allocates a span from string 'str' and replaces /' with /"
/// Returns a new string
/// This method looks for a specific byte: ASCII "'", '39'
/// </summary>
public class ValidateNoApostrophe : IJsonValidationStrategy {
	public JsonStrategyResult ValidateStrategy (System.Span<byte> json) {
		for (int i = 0; i < json.Length; i++) {
			if (json[i] == Apostrophe) {
				json[i] = Quotation;
			}
		}

		return JsonStrategyResult.Good;
	}

	private const byte Apostrophe = 39;
	private const byte Quotation = 34;
}