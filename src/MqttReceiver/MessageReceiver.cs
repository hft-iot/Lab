using System.Text.Json;
using Interfaces;
using IoTHubConnector;
using Microsoft.Extensions.Configuration;
using MQTTnet;
using StreamProcessing;

namespace MqttReceiver
{
    public class MessageReceiver(
        ILogger<MessageReceiver> logger,
        StreamProcessor processor,
        Connector iotHubConnector,
        IConfiguration configuration
    ) : BackgroundService
    {
        private readonly ILogger<MessageReceiver> _logger = logger;
        private readonly StreamProcessor processor = processor;
        private readonly Connector _iotHubConnector = iotHubConnector;
        private readonly string _mqttHost =
            configuration.GetValue<string>("MqttHost") ?? "localhost";
        private readonly MqttClientFactory _mqttFactory = new();
        private IMqttClient _mqttClient = null!;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _mqttClient = _mqttFactory.CreateMqttClient();
            var mqttClientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_mqttHost, 1883)
                .Build();
            await _mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

            _iotHubConnector.ControlMessageReceived += HandleControlMessageReceivedEvent;
            _iotHubConnector.ControlTemperatureReceived += HandleControlTemperatureReceivedEvent;

            _mqttClient.ApplicationMessageReceivedAsync += async (e) =>
            {
                _logger.LogInformation(
                    "Received message from topic: {topic}",
                    e.ApplicationMessage.Topic
                );
                var message = JsonSerializer.Deserialize<IotMessage<double>>(
                    e.ApplicationMessage.ConvertPayloadToString()
                );
                if (message is null)
                {
                    _logger.LogError("Received null message");
                    return;
                }

                _logger.LogInformation(
                    "Received Message: {message}, Timestamp: {Timestamp}, Type: {Type}",
                    message.Message,
                    message.Timestamp,
                    message.Type
                );
                await processor.HandleMessage(message);
                return;
            };

            await _mqttClient.SubscribeAsync(
                new MqttTopicFilterBuilder().WithTopic("temperature/living_room").Build(),
                stoppingToken
            );

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }

            await _mqttClient.DisconnectAsync(
                new MqttClientDisconnectOptionsBuilder()
                    .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                    .Build(),
                stoppingToken
            );
        }

        private async void HandleControlMessageReceivedEvent(
            object? sender,
            ControlMessageReceivedEventArgs e
        )
        {
            if (e.Message.Temperature is null)
            {
                // If the control message has no temperature, call GetTwinAsync in the connector,
                // read desired.temperature, use it when available, otherwise fall back to 20.
            }

            await _mqttClient.PublishAsync(
                new MqttApplicationMessageBuilder()
                    .WithTopic("temperature/living_room/control")
                    .WithPayload(JsonSerializer.Serialize(e.Message))
                    .Build()
            );
        }

        private void HandleControlTemperatureReceivedEvent(
            object? sender,
            ControlTemperatureReceivedEventArgs e
        )
        {
            // Publish the received temperature to a dedicated MQTT topic.
        }
    }
}
