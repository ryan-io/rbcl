using System.Text;
using System.Text.Json;

namespace rbcl;

[Flags, Serializable]
public enum JsonValidatorErrorType : byte {
	None = 1 << 0,
	Validation = 1 << 1,
	Parsing = 1 << 2,
	NullWhitespaceOrEmpty = 1 << 3,
	Unspecified = 1 << 4,
}

/// <summary>
/// Uses 'System.Text.Json'
/// Internally invokes 'JObject.Parse' on the provided string, 'json'
/// There is a try-catch block to handle 'JsonReaderException' exception
/// The dependency on the ErrorOr package allows a discriminated return type
///		that contains an error message and state
/// Typical JSON formatting is expressed as the following (example):
/// "{
///		"Id":1,
///		"Name": "Test",
///		"Payload":"12, 323, 232"
/// }"
/// For this method, the double parenthesis (") need to be replaced with single parenthesis (')
/// "{
///		'Id':1,
///		'Name': 'Test',
///		'Payload': '12, 323, 232'
/// }"
/// </summary>
public interface IJsonValidator {
	JsonValidationResult Validate (ref string json);
}

public interface IJsonValidationStrategy {
	// return type 'JsonValidatorErrorType' should return '0' if no errors were encountered,
	// otherwise, the string should contain a detailed message of the issue 
	JsonStrategyResult ValidateStrategy (ref System.Span<byte> json);
}

public readonly struct JsonStrategyResult {
	public JsonValidatorErrorType ErrorType { get; init; }
	public string? ErrorMessage { get; init; }
}

/// <inheritdoc/>>
public class JsonValidator : IJsonValidator {
	/// <summary>
	/// This constructor accepts a 'HashSet' of validation strategies
	/// A hash set is used to ensure only one validator type instance is run
	/// </summary>
	/// <param name="strategies"></param>
	public JsonValidator (
		HashSet<IJsonValidationStrategy> strategies) {
		ArgumentNullException.ThrowIfNull(strategies);

		foreach (var strategy in strategies) {
			_strategies.Add(strategy);
		}
	}

	/// <summary>
	/// Takes a string and allocates enough memory on the stack in the form of a 'Span'
	/// Returns a new instance of a string after the data transformations are applied
	/// The internals only manipulate a mutatable 'Span' before returning a new string
	/// The byte span is allocated via stackalloc
	/// The span is then populated using 'CopyTo' (extension method byte arrays)
	///		stemming from the returned byte array from 'Encoding.ASCII.GetBytes'
	/// </summary>
	/// <param name="json">reference to a string, stackallocs a span for transforming</param>
	public JsonValidationResult Validate (ref string json) {
		_errors.Clear();    // reset the errors each invocation of 'Validate'

		// this stack allocated span will be removed from memory when it goes out of scope
		// between the instantiation of this span and the end of it's lifetime (the '}' at
		// the end of this method) will pass this span through a collection of validators.
		// these validators will make changes in memory to this span
		System.Span<byte> span = stackalloc byte[json.Length];
		(Encoding.ASCII.GetBytes(json)).CopyTo(span);   // this is how we populate our span

		// the string needs to have some data... return early if there isn't any
		if (json.Length < 1) {
			AddError(JsonValidatorErrorType.NullWhitespaceOrEmpty, "An empty string was provided.");
			return ValidationComplete(ref span);
		}

		try {
			foreach (var strategy in _strategies) {
				var error = strategy.ValidateStrategy(ref span);

				if (!error.ErrorType.HasFlag(JsonValidatorErrorType.None)) {
					AddError(error.ErrorType, error.ErrorMessage);
				}
			}
		}
		catch (JsonException e) {
			// this is a bit of a generic 'catch all' when using 'System.Text.Json'
			AddError(JsonValidatorErrorType.Parsing, e.Message);
		}

		return ValidationComplete(ref span);
	}

	private void AddError (JsonValidatorErrorType type, string? errorMsg) {
		if (string.IsNullOrWhiteSpace(errorMsg)) {
			errorMsg = "A generic and/or undefined error was encountered.";
		}

		if (_errors.TryGetValue(type, out var error)) {
			error.Add(errorMsg);
		}
		else {
			_errors.Add(type, [errorMsg]);
		}
	}

	private JsonValidationResult ValidationComplete (ref System.Span<byte> json) {
		var hadErrors = _errors.Any();
		var outputJson = hadErrors ? string.Empty : Encoding.ASCII.GetString(json.ToArray());

		return new JsonValidationResult() {
			HadErrors = hadErrors,
			Json = outputJson,
			Errors = _errors
		};
	}

	private readonly Dictionary<JsonValidatorErrorType, HashSet<string>> _errors = new();
	private readonly HashSet<IJsonValidationStrategy> _strategies = new();
}