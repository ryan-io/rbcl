using FluentAssertions;
using NSubstitute;
using System.Text;

namespace rbcl.tests.unit {
	public class JsonValidatorUnitTests {
		private JsonValidator _sut;
		private JsonStrategyMap _processStrategies;
		private JsonStrategyMap _preprocessStrategies;

		public JsonValidatorUnitTests () {
			_processStrategies =
			[
				Substitute.For<IJsonValidationStrategy>(),
				Substitute.For<IJsonValidationStrategy>(),
				Substitute.For<IJsonValidationStrategy>()
			];

			_preprocessStrategies =
			[
				Substitute.For<IJsonValidationStrategy>(),
				Substitute.For<IJsonValidationStrategy>(),
				Substitute.For<IJsonValidationStrategy>()
			];

			_sut = new JsonValidator(_processStrategies);
		}

		[Fact]
		void Strategies_ShouldNotBeNull_WhenCreatingNew () {
			// Arrange
			var sut = new JsonValidator(_processStrategies);

			// Act
			var strategies = sut.ProcessStrategies;

			// Assert
			strategies.Should().NotBeNullOrEmpty();
		}


		[Fact]
		void Strategies_ShouldHaveIdenticalCount_ToInstantiatedSet () {
			// Arrange
			var strategies = _sut.ProcessStrategies;

			// Act
			var count = strategies.Count();

			// Assert
			count.Should().Be(_processStrategies.Count);
			count.Should().BeGreaterThan(0);
		}

		[Fact]
		void Constructor_ShouldThrowArgumentNullException_WhenCreatingNewWithNullSet () {
			// Arrange
			var action = () => new JsonValidator(default!);

			// Act


			// Assert
			action.Should().ThrowExactly<ArgumentNullException>();
		}

		[Fact]
		void Validate_ThrowsArgumentException_WhenJsonStringIsNullOrEmpty () {
			// arrange
			var str = string.Empty;

			// act
			var action = () => _sut.Validate(ref str);

			// assert
			action.Should().ThrowExactly<ArgumentException>();
		}

		[Theory]
		[InlineData("{\"Id\":1,\"Name\": \"Test\",\"Payload\":\"12, 323, 232\"}")]
		void Validate_ReturnsPositiveResult_WhenValidJsonIsPassed (string json) {
			// arrange
			System.Span<byte> span1 = stackalloc byte[json.Length];
			(Encoding.ASCII.GetBytes(json)).CopyTo(span1);

			//TODO: this is okay for now... 
			var s1 = new ValidateIsJsonObject();//.ValidateStrategy(ref span1);
			_processStrategies = [s1];
			_sut = new JsonValidator(_processStrategies);

			// act
			var resultFunc = () => _sut.Validate(ref json);

			// assert
			resultFunc.Should().NotThrow();
			resultFunc.Invoke().Should().NotBeNull();
			resultFunc.Invoke().Errors.Should().BeNullOrEmpty();
			resultFunc.Invoke().Json.Should().NotBeNullOrEmpty();
			resultFunc.Invoke().HadErrors.Should().BeFalse();
		}

		[Theory]
		[InlineData("{\"Id\":1,\"Name\": 'Test\",\"Payload\":\"12, 323, 232\"}")]
		void Validate_ReturnsNegativeResult_WhenInValidJsonIsPassed (string json) {
			// arrange
			System.Span<byte> span1 = stackalloc byte[json.Length];
			(Encoding.ASCII.GetBytes(json)).CopyTo(span1);

			//TODO: this is okay for now... 
			var s1 = new ValidateIsJsonObject();//.ValidateStrategy(ref span1);
			_processStrategies = [s1];
			_sut = new JsonValidator(_processStrategies);

			// act
			var resultFunc = () => _sut.Validate(ref json);

			// assert
			resultFunc.Should().NotThrow();
			resultFunc.Invoke().Should().NotBeNull();
			resultFunc.Invoke().Errors.Should().NotBeNullOrEmpty();
			resultFunc.Invoke().Json.Should().BeNullOrEmpty();
			resultFunc.Invoke().HadErrors.Should().BeTrue();
		}


		[Theory]
		[InlineData("")]
		[InlineData(" ")]
		void Validate_ThrowsArgumentException_WhenStringIsNullOrEmptyWhitespace (string json) {
			// arrange
			System.Span<byte> span1 = stackalloc byte[json.Length];
			(Encoding.ASCII.GetBytes(json)).CopyTo(span1);

			//TODO: this is okay for now... 
			var s1 = new ValidateIsJsonObject();//.ValidateStrategy(ref span1);
			_processStrategies = [s1];
			_sut = new JsonValidator(_processStrategies);

			// act
			var resultFunc = () => _sut.Validate(ref json);

			// assert
			resultFunc.Should().Throw<ArgumentException>();
		}

		/// <summary>
		/// This bastardized piece of code attempts to pin memory on the stack for a byte pointer
		/// that is created from a 'Span' that is stack allocated wrt 'json.Length'
		/// </summary>
		[Theory]
		[InlineData("{\"Id\":1,\"Name\": 'Test\",\"Payload\":\"12, 323, 232\"}")]
		unsafe void Validate_ShouldRunPreprocessors_WhenInitialized (string json) {
			// arrange
			System.Span<byte> span1 = stackalloc byte[json.Length];
			fixed (byte* ptr = &span1.GetPinnableReference()) {
				(Encoding.ASCII.GetBytes(json)).CopyTo(span1);
				//TODO: this is okay for now... 
				var veryBadPtrToCreate = ptr;
				var s1 = Substitute.For<IJsonValidationStrategy>();
				var result = new JsonStrategyResult() { ErrorMessage = string.Empty, ErrorType = JsonValidatorErrorType.None };
				//TODO: is there a mocking library that supports spans?

				//https://stackoverflow.com/questions/59605908/what-is-a-working-alternative-to-being-unable-to-pass-a-spant-into-lambda-expr
				var jsonLocal = json;
				//var action = () => {
				//	var spanToPass = new System.Span<byte>(veryBadPtrToCreate, jsonLocal.Length);
				//	return s1.ValidateStrategy(spanToPass);
				//};

				_preprocessStrategies = [s1];
				_sut = new JsonValidator(_processStrategies);
				//_sut.Validate(ref json);

				// assert

			}
		}
	}
}