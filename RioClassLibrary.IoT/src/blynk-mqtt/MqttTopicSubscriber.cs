#if LOGGING
using Microsoft.Extensions.Logging;
#endif
using MQTTnet;
using MQTTnet.Client;

namespace rbcl.iot;

public class MqttTopicSubscriber {
#if LOGGING
	public MqttTopicSubscriber (ILogger? logger) {
		_logger = logger;
	}
#endif

	public MqttTopicSubscriber () { }

	public MqttClientSubscribeOptions Subscribe (MqttSubcription subcription) {
		var mqttFactory = new MqttFactory();
		var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder();

		if ((subcription & MqttSubcription.None) != 0) {
#if LOGGING
			_logger?.LogInformation("No subscriptions for Blynk via MQTT will be registered.");
#endif
			return mqttSubscribeOptions.Build();
		}

		if ((subcription & MqttSubcription.Downlink) != 0) {
#if LOGGING
			_logger?.LogInformation("Subscribed to downlink MQTT messages from Blynk.");
#endif
			mqttSubscribeOptions.WithTopicFilter(filter => filter.WithTopic("downlink/#"));
		}

		if ((subcription & MqttSubcription.Uplink) != 0) {
#if LOGGING
			_logger?.LogInformation("Subscribed to uplink MQTT messages from Blynk.");
#endif
			mqttSubscribeOptions.WithTopicFilter(filter => filter.WithTopic("uplink/#"));
		}

		return mqttSubscribeOptions.Build();
	}

#if LOGGING
	private readonly ILogger? _logger;
#endif
}