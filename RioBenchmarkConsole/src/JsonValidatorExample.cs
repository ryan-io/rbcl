namespace rbcl.console {
	internal class JsonValidatorExample {
		public void Run () {
			var replace = new ReplaceApostropheWithQuote();
			var json = "{'Id':1,'Name': 'Test','Payload':'12, 323, 232'}";
			var validatedStr = replace.Modify(ref json);

			// create a new instance of 'JsonValidator'
			// the primary constructor takes a 'HashSet<IJsonValidationStrategy>'
			// simply pass an instance to the constructor containing concrete
			//		implementations of 'IJsonValidationStrategy'
			var validator = new JsonValidator([new ValidateIsJsonObject()]);

			// invoke the 'Validate' method
			var result = validator.Validate(ref validatedStr);

			// check if there were errors
			var hadErrors = result.HadErrors;

			// if hadErrors is true, we can query a dictionary (nullable) of error strings
			var errors = result.Errors;
			// if 'result.HadErrors' is false, then 'result.Errors' will be null

			// the new validated string will be return if 'result.HadErrors' is false
			// otherwise, 'result.Json' will be 'string.Empty'
			var validatedJson = result.Json;
		}
	}
}
