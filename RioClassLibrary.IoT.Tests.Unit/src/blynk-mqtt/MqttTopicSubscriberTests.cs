#if LOGGING
using Microsoft.Extensions.Logging;
#endif
using FluentAssertions;

namespace rbcl.iot.tests.unit {

	public class MqttTopicSubscriberTests {
#if LOGGING
		private readonly ILogger<MqttTopicSubscriber> _logger = Substitute.For<ILogger<MqttTopicSubscriber>>();
#endif
		private readonly MqttTopicSubscriber _sut;

		public MqttTopicSubscriberTests () {
#if LOGGING
			_sut = new MqttTopicSubscriber(_logger);
#else
			_sut = new MqttTopicSubscriber();
#endif
		}

		[Fact]
		public void Subscribe_ShouldReturnOptionsWithNoSubscriptions_WhenSubcriptionIsNone () {
			// Arrange
			var subscription = MqttSubcription.None;

			// Act
			var result = _sut.Subscribe(subscription);

			// Assert
			result.Should().NotBeNull();
			result.TopicFilters.Should().BeEmpty();
		}

		[Fact]
		public void Subscribe_ShouldReturnOptionsWithDownlinkSubscription_WhenSubcriptionHasDownlinkFlag () {
			// Arrange
			var subscription = MqttSubcription.Downlink;

			// Act
			var result = _sut.Subscribe(subscription);

			// Assert
			result.Should().NotBeNull();
			result.TopicFilters.Should().HaveCount(1);
			result.TopicFilters[0].Topic.Should().Be("downlink/#");
#if LOGGING
			_logger.Received().LogInformation("Subscribed to downlink MQTT messages from Blynk.");
#endif
		}

		[Fact]
		public void Subscribe_ShouldReturnOptionsWithUplinkSubscription_WhenSubcriptionHasUplinkFlag () {
			// Arrange
			var subscription = MqttSubcription.Uplink;

			// Act
			var result = _sut.Subscribe(subscription);

			// Assert
			result.Should().NotBeNull();
			result.TopicFilters.Should().HaveCount(1);
			result.TopicFilters[0].Topic.Should().Be("uplink/#");
#if LOGGING
			_logger.Received().LogInformation("Subscribed to uplink MQTT messages from Blynk.");
#endif
		}

		[Fact]
		public void Subscribe_ShouldReturnOptionsWithBothSubscriptions_WhenSubcriptionHasBothFlags () {
			// Arrange
			var subscription = MqttSubcription.Downlink | MqttSubcription.Uplink;

			// Act
			var result = _sut.Subscribe(subscription);

			// Assert
			result.Should().NotBeNull();
			result.TopicFilters.Should().HaveCount(2);
			result.TopicFilters[0].Topic.Should().Be("downlink/#");
			result.TopicFilters[1].Topic.Should().Be("uplink/#");
#if LOGGING
			_logger.Received().LogInformation("Subscribed to downlink MQTT messages from Blynk.");
			_logger.Received().LogInformation("Subscribed to uplink MQTT messages from Blynk.");
#endif
		}
	}
}