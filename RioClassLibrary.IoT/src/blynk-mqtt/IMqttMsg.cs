using MQTTnet;

namespace rbcl.iot;

/* example with concrete IMqttMsg:
 *  public async Task Signal (IMqttMsg msgPayload, CancellationToken token = default) {
   	try {
   		await Task.Delay(TimeSpan.FromSeconds(PollLimitSeconds), token);
   		await _client.PublishAsync(msgPayload.Get(), token);
   	}
   	catch (Exception e) {
   		_logger?.LogError(e.Message);
   		throw;
   	}
   }
 */

public interface IMqttMsg {
	MqttApplicationMessage Get ();
}