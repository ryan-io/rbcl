using rbcl.json;

namespace rbcl.console;

internal class JsonValidatorExample {
	public void Run () {
		var json = "{'Id':1,'Name': 'Test','Payload':'12, 323, 232'}";

#if DEBUG
		Console.WriteLine($"Validating the following json: {json}");
#endif

		// create a new instance of 'JsonValidator'
		// the primary constructor takes a 'HashSet<IJsonValidationStrategy>'
		// simply pass an instance(s) to the constructor containing concrete
		//		implementations of 'IJsonValidationStrategy'
		// the default constructor takes a hashset for process strategies and preprocess strategies (optional)
		var validator = new JsonValidator(
			[new ValidateIsJsonObject()],
			[new ValidateNoApostrophe()]);

		// invoke the 'Validate' method
		var result = validator.Validate(ref json);

		// check if there were errors
		var hadErrors = result.HadErrors;

		// if hadErrors is true, we can query a dictionary (nullable) of error strings
		var errors = result.Errors;
		// if 'result.HadErrors' is false, then 'result.Errors' will be null

		// the new validated string will be return if 'result.HadErrors' is false
		// otherwise, 'result.Json' will be 'string.Empty'
		var validatedJson = result.Json;

#if DEBUG

		Console.WriteLine($"Validation resulted in the following json: {validatedJson}");

#endif
	}
}

/// <summary>
/// To create your own validation strategies, create a new class and implement 'IJsonValidationStrategy'
/// The method to define implementation for is 'ValidateStrategy(System.Span<byte> json)'
/// Take note -> this span is mutable; you are directly modifying the byte values for a contiguous
///		portion of memory allocated on the stack
/// </summary>
public class MyJsonValidationExample : IJsonValidationStrategy {
	public JsonStrategyResult ValidateStrategy (System.Span<byte> json) {
		throw new NotImplementedException();
	}
}