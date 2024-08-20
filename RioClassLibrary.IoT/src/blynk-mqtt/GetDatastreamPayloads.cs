using MQTTnet;

namespace rbcl.iot;

/// <summary>
/// Gets the following payload:
///		Topic: "ds" (downstream)
///		Payload: "temperature_sys_1" (looks for this identifier
/// </summary>
public class GetDatastreamPayloads : IMqttMsg {

	public MqttApplicationMessage Get () {
		var applicationMessage = new MqttApplicationMessageBuilder()
			.WithTopic("get/ds")
			.WithPayload("temperature_sys_1")
			.Build();

		return applicationMessage;
	}
}

/// <summary>
///		Topic: "ds" (downstream)
///		Payload: "timestamp" (looks for this identifier)
/// </summary>
public class GetTimestampPayload : IMqttMsg {
	public MqttApplicationMessage Get () {
		var applicationMessage = new MqttApplicationMessageBuilder()
			.WithTopic("get/ds")
			.WithPayload("timestamp")
			.Build();

		return applicationMessage;
	}
}