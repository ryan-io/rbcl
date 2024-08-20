﻿using MQTTnet;
using MQTTnet.Client;
#if LOGGING
	using Microsoft.Extensions.Logging;
#endif

namespace rbcl.iot {
	// TODO: THIS CLASS NEEDS DOCUMENTATION. I DID NOT WRITE THIS CLASS AS WELL AS I SHOULD HAVE
	/// <summary>
	/// Represents a Blynk MQTT client.
	/// </summary>
	public class BlynkMqttClient : IDisposable, IBlynkMqttClient {
#if LOGGING
		private readonly ILogger? _logger;
#endif
		private readonly IMqttClient _client;
		private readonly IBlynkMqttClientConfig _config;

		/// <summary>
		/// Event for subscribing to any available MQTT message types
		/// </summary>
		public event Action<MqttApplicationMessageReceivedEventArgs>? MessageReceived;

#if LOGGING
		/// <summary>
		/// Initializes a new instance of the <see cref="BlynkMqttClient"/> class.
		/// </summary>
		/// <param name="client">The MQTT client.</param>
		/// <param name="config">The Blynk MQTT client configuration.</param>
		/// <param name="logger">The logger.</param>
		public BlynkMqttClient (IMqttClient? client, IBlynkMqttClientConfig config, ILogger? logger = default) {
			if (client == null) {
				var factory = new MqttFactory();
				_client = factory.CreateMqttClient();
			}
			else {
				_client = client;
			}

			_config = config;
			_logger = logger;
		}
#endif

		/// <summary>
		/// Initializes a new instance of the <see cref="BlynkMqttClient"/> class.
		/// </summary>
		/// <param name="client">The MQTT client.</param>
		/// <param name="config">The Blynk MQTT client configuration.</param>
		public BlynkMqttClient (IMqttClient? client, IBlynkMqttClientConfig config) {
			if (client == null) {
				var factory = new MqttFactory();
				_client = factory.CreateMqttClient();
			}
			else {
				_client = client;
			}

			_config = config;
		}

		/// <summary>
		/// Attempts to establish a connection to a Blynk IoT device using the provided credentials via MQTT.
		/// </summary>
		/// <param name="subscription">The MQTT subscription.</param>
		/// <param name="token">The cancellation token.</param>
		/// <returns>A task representing the connection result.</returns>
		public async Task<bool> Connect (MqttSubcription subscription = (MqttSubcription.Uplink | MqttSubcription.Downlink), CancellationToken token = default) {
			var optionsBuilder = new BlynkMqttOptionsBuilder();
			var mqttClientOptions = optionsBuilder.BuildOptions(_config);

#if LOGGING
			if (_logger != null) {
				_client.ConnectedAsync += _ => {
					_logger.LogInformation("Connected to {iot}", _config.Broker);
					return Task.CompletedTask;
				};

				_client.DisconnectedAsync += _ => {
					_logger.LogInformation("Disconnected from {iot}", _config.Broker);
					return Task.CompletedTask;
				};
			}
#endif

			// the '-1' is due to a dedicated background worker thread already polling for MQTT messages

			_client.ApplicationMessageReceivedAsync += async e => {
				e.AutoAcknowledge = false;
				await e.AcknowledgeAsync(_cancellation.Token);
				MessageReceived?.Invoke(e);
			};

			var result = await _client.ConnectAsync(mqttClientOptions, token);

			MqttTopicSubscriber topicSubscriber;
#if LOGGING
			topicSubscriber = new MqttTopicSubscriber(_logger);
#else
			topicSubscriber = new MqttTopicSubscriber();
#endif

			await _client.SubscribeAsync(topicSubscriber.Subscribe(subscription), token);

			return result.IsSessionPresent;
		}

		/// <summary>
		/// Disconnects from the MQTT server (broker).
		/// </summary>
		/// <param name="token">The cancellation token.</param>
		/// <returns>A task representing the disconnection result.</returns>
		public async Task Disconnect (CancellationToken token = default) {
			await _client.DisconnectAsync(cancellationToken: token);
		}

		/// <summary>
		/// Disposes the MQTT client.
		/// </summary>
		public void Dispose () {
			if (IsDisposed) return;
			_cancellation.Cancel();
			_cancellation.Dispose();
			MessageReceived = default;
			IsDisposed = true;
			_client.Dispose();
		}

		/// <summary>
		/// Gets or sets a value indicating whether the MQTT client is disposed.
		/// </summary>
		private bool IsDisposed { get; set; }

		private readonly CancellationTokenSource _cancellation = new();
	}
}