namespace rbcl;

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
	public Dictionary<JsonValidatorErrorType, HashSet<string>>? Errors { get; init; }
}