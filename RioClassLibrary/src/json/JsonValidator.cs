using System.Text;
using System.Text.Json;

namespace rbcl.json;

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
	IReadOnlySet<IJsonValidationStrategy> ProcessStrategies { get; }
	JsonValidationResult Validate (ref string json);
}

public interface IJsonValidationStrategy {
	// return type 'JsonValidatorErrorType' should return '0' if no errors were encountered,
	// otherwise, the string should contain a detailed message of the issue 
	JsonStrategyResult ValidateStrategy (System.Span<byte> json);
}

public readonly struct JsonStrategyResult {
	public JsonValidatorErrorType ErrorType { get; init; }
	public string? ErrorMessage { get; init; }

	public static JsonStrategyResult Good =>
		new() { ErrorMessage = string.Empty, ErrorType = JsonValidatorErrorType.None };
}

/// <summary>
/// Struct wrapper for validating JSON
/// </summary>
public readonly struct JsonValidationResult {
	public bool HadErrors { get; init; }
	public string Json { get; init; }

	/// <summary>
	/// A dictionary containing a byte (JsonValidatorErrorType) and a hashset of strings
	///		that contain errors encountered during validation
	/// </summary>
	public JsonValidationErrorMap? Errors { get; init; }
}

[Serializable]
public class JsonValidationErrorMap : Dictionary<JsonValidatorErrorType, HashSet<string>> { }

[Serializable]
public class JsonStrategyMap : HashSet<IJsonValidationStrategy> { }

/// <inheritdoc/>>
public class JsonValidator : IJsonValidator {
	/// <summary>
	/// This constructor accepts a 'HashSet' of validation strategies
	/// A hash set is used to ensure only one validator type instance is run
	/// A second hash set is pass to act as a collection of strategies that are
	///		to be run before the primary process strategies
	/// This hashset is deemed the 'preprocess strategies' set
	/// </summary>
	public JsonValidator (
		JsonStrategyMap processStrategies,
		JsonStrategyMap? preprocessStrategies = default) {
		ArgumentNullException.ThrowIfNull(processStrategies);
		_processStrategies = processStrategies;
		_preprocessStrategies = preprocessStrategies;
	}

	/// <summary>
	/// Returns the list of strategies used to construct the object
	/// </summary>
	public IReadOnlySet<IJsonValidationStrategy> ProcessStrategies => _processStrategies;

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
		ArgumentException.ThrowIfNullOrWhiteSpace(json);
		_errors.Clear();    // reset the errors each invocation of 'Validate'

		// this stack allocated span will be removed from memory when it goes out of scope
		// between the instantiation of this span and the end of it's lifetime (the '}' at
		// the end of this method) will pass this span through a collection of validators.
		// these validators will make changes in memory to this span
		System.Span<byte> span = stackalloc byte[json.Length];
		(Encoding.ASCII.GetBytes(json)).CopyTo(span);   // this is how we populate our span

		try {
			if (ShouldRunPreprocessors) {
				ValidateSet(ref span, _preprocessStrategies!); // see 'ShouldRunPreprocessors'
			}

			span = ValidateSet(ref span, _processStrategies);
		}
		catch (JsonException e) {
			// this is a bit of a generic 'catch all' when using 'System.Text.Json'
			AddError(JsonValidatorErrorType.Parsing, e.Message);
		}

		return ValidationComplete(ref span);
	}

	private System.Span<byte> ValidateSet (
		ref System.Span<byte> span,
		HashSet<IJsonValidationStrategy> validationStrategies) {
		foreach (var strategy in validationStrategies) {
			var error = strategy.ValidateStrategy(span);

			if (!error.ErrorType.HasFlag(JsonValidatorErrorType.None)) {
				AddError(error.ErrorType, error.ErrorMessage);
			}
		}

		return span;
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

	private bool ShouldRunPreprocessors => _preprocessStrategies is { Count: > 0 };

	private readonly JsonValidationErrorMap _errors = new();
	private readonly JsonStrategyMap? _preprocessStrategies;
	private readonly JsonStrategyMap _processStrategies;
}