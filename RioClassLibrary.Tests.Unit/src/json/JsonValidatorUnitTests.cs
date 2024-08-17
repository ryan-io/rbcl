using FluentAssertions;
using NSubstitute;
using System.Text;

namespace rbcl.tests.unit {
	public class JsonValidatorUnitTests {
		private JsonValidator _sut;
		private HashSet<IJsonValidationStrategy> _strategies;

		public JsonValidatorUnitTests () {
			_strategies =
			[
				Substitute.For<IJsonValidationStrategy>(),
				Substitute.For<IJsonValidationStrategy>(),
				Substitute.For<IJsonValidationStrategy>()
			];

			_sut = new JsonValidator(_strategies);
		}

		[Fact]
		void Strategies_ShouldNotBeNull_WhenCreatingNew () {
			// Arrange
			var sut = new JsonValidator(_strategies);

			// Act
			var strategies = sut.Strategies;

			// Assert
			strategies.Should().NotBeNullOrEmpty();
		}


		[Fact]
		void Strategies_ShouldHaveIdenticalCount_ToInstantiatedSet () {
			// Arrange
			var strategies = _sut.Strategies;

			// Act
			var count = strategies.Count();

			// Assert
			count.Should().Be(_strategies.Count);
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
			_strategies = [s1];
			_sut = new JsonValidator(_strategies);

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
			_strategies = [s1];
			_sut = new JsonValidator(_strategies);

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
			_strategies = [s1];
			_sut = new JsonValidator(_strategies);

			// act
			var resultFunc = () => _sut.Validate(ref json);

			// assert
			resultFunc.Should().Throw<ArgumentException>();

		}
	}
}