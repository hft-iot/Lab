using System.Text.Json;
using Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;

namespace MqttSenders
{
    public class MqttDeviceSimulator(
        ILogger<MqttDeviceSimulator> logger,
        IConfiguration configuration
    ) : BackgroundService
    {
        private readonly ILogger<MqttDeviceSimulator> _logger = logger;
        private readonly string _mqttHost =
            configuration.GetValue<string>("MqttHost") ?? "localhost";
        private readonly Random _random = new();
        private readonly MqttClientFactory _mqttFactory = new();
        private bool _heatingMode = true;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var mqttClient = _mqttFactory.CreateMqttClient();
            var mqttClientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_mqttHost, 1883)
                .Build();
            await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

            var discoveryMessageLoop = StartDiscoveryMessageLoop(mqttClient, stoppingToken);

            mqttClient.ApplicationMessageReceivedAsync += HandleIncomingMessageEvent;
            await mqttClient.SubscribeAsync(
                new MqttTopicFilterBuilder().WithTopic("temperature/living_room/control").Build(),
                stoppingToken
            );

            var temperature = 21f;

            while (!stoppingToken.IsCancellationRequested)
            {
                temperature = _heatingMode
                    ? temperature + _random.NextSingle()
                    : temperature - _random.NextSingle();
                if (temperature > 25)
                    _heatingMode = false;
                if (temperature < 18)
                    _heatingMode = true;
                var iotMessage = new IotMessage<double>(
                    temperature,
                    DateTimeOffset.Now,
                    "telemetry"
                );
                var payload = JsonSerializer.Serialize(iotMessage);

                var applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic("temperature/living_room")
                    .WithPayload(payload)
                    .Build();

                _logger.LogInformation(
                    "Publishing Message at {Timestamp} with Temperature: {Temperature}",
                    DateTimeOffset.Now,
                    temperature
                );

                await mqttClient.PublishAsync(applicationMessage, CancellationToken.None);

                await Task.Delay(1000, stoppingToken);
            }

            await mqttClient.DisconnectAsync(
                new MqttClientDisconnectOptionsBuilder()
                    .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                    .Build(),
                stoppingToken
            );
        }

        private Task HandleIncomingMessageEvent(MqttApplicationMessageReceivedEventArgs e)
        {
            _logger.LogInformation(
                "Received message from topic: {topic}",
                e.ApplicationMessage.Topic
            );
            var message = JsonSerializer.Deserialize<ControlMessage>(
                e.ApplicationMessage.ConvertPayloadToString()
            );
            if (message is null)
            {
                _logger.LogError("Received null message");
                return Task.CompletedTask;
            }

            _logger.LogInformation("Received Control Message: {message}", message.CommandName);
            HandleControlMessage(message);
            return Task.CompletedTask;
        }

        private void HandleControlMessage(ControlMessage message)
        {
            //implement this method to switch between heating and cooling mode
        }

        private async Task StartDiscoveryMessageLoop(
            IMqttClient mqttClient,
            CancellationToken cancellationToken
        )
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var payload =
                    @"
                    {
                        ""name"": ""TemperatureSimulator"",
                        ""device_class"":""temperature"",
                        ""state_topic"":""temperature/living_room"",
                        ""unique_id"":""11C1C0DA-1551-4755-8AAD-CFB1D21FC973"",
                        ""device"":{
                            ""identifiers"":[
                                ""11C1C0DA-1551-4755-8AAD-CFB1D21FC973""
                                ],
                            ""name"":""TemperatureSimulator""
                        },
                        ""value_template"": ""{{ value_json.Value | round(2) }}"",
                        ""max"": 200,
                        ""min"": -100,
                        ""unit_of_measurement"": ""°C""
                    }
                    ";

                var applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic("homeassistant/sensor/11C1C0DA-1551-4755-8AAD-CFB1D21FC973/config")
                    .WithPayload(payload)
                    .Build();

                if (mqttClient != null && mqttClient.IsConnected)
                {
                    _logger.LogInformation("Publishing Discovery Message");
                    await mqttClient.PublishAsync(applicationMessage, CancellationToken.None);
                }
                else
                {
                    _logger.LogInformation(
                        "Cannot send HomeAssistant Discovery Message because client is not connected"
                    );
                }
                await Task.Delay(10000, cancellationToken);
            }
        }
    }
}
