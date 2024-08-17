using System.Text;
using System.Text.Json;

namespace rbcl;

/// <summary>
/// Validates whether the JSON string is in an appropriate format
/// </summary>
public class ValidateIsJsonObject : IJsonValidationStrategy {
	public JsonStrategyResult ValidateStrategy (ref System.Span<byte> json) {
		JsonValidatorErrorType errorType = JsonValidatorErrorType.None;
		string errorMessage = string.Empty;

		try {
			var str = Encoding.UTF8.GetString(json);
			using var _ = JsonDocument.Parse(str);
		}
		catch (JsonException e) {
			errorType = JsonValidatorErrorType.Validation;
			errorMessage = e.Message;
		}

		return new JsonStrategyResult() {
			ErrorType = errorType,
			ErrorMessage = errorMessage
		};
	}
}