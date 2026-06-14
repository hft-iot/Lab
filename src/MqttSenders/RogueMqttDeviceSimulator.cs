using System.Text.Json;
using Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;

namespace MqttSenders
{
    public class RogueMqttDeviceSimulator(
        ILogger<RogueMqttDeviceSimulator> logger,
        IConfiguration configuration
    ) : BackgroundService
    {
        private readonly ILogger<RogueMqttDeviceSimulator> _logger = logger;
        private readonly MqttConnectionSettings _rogueConnectionSettings =
            MqttConnectionSettings.FromConfiguration(configuration.GetSection("RogueConnection"));
        private readonly Random _random = new();
        private readonly MqttClientFactory _mqttFactory = new();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var mqttClient = _mqttFactory.CreateMqttClient();
            var mqttClientOptions = MqttClientOptionsFactory.Create(_rogueConnectionSettings);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!mqttClient.IsConnected)
                    {
                        _logger.LogInformation(
                            "Rogue tries to connect to {Host}:{Port} (TLS: {UseTls})",
                            _rogueConnectionSettings.Host,
                            _rogueConnectionSettings.Port,
                            _rogueConnectionSettings.UseTls
                        );

                        await mqttClient.ConnectAsync(mqttClientOptions, stoppingToken);
                    }

                    var manipulatedTemperature = _random.Next(85, 141);
                    var evilMessage = new IotMessage<double>(
                        manipulatedTemperature,
                        DateTimeOffset.UtcNow,
                        "evil-telemetry"
                    );

                    await mqttClient.PublishAsync(
                        new MqttApplicationMessageBuilder()
                            .WithTopic("temperature/living_room")
                            .WithPayload(JsonSerializer.Serialize(evilMessage))
                            .Build(),
                        stoppingToken
                    );

                    _logger.LogWarning(
                        "Rogue published manipulated telemetry: {Temperature}",
                        manipulatedTemperature
                    );

                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Rogue could not connect or publish. This is expected when the broker enforces valid client certificates."
                    );

                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            if (mqttClient.IsConnected)
            {
                await mqttClient.DisconnectAsync(
                    new MqttClientDisconnectOptionsBuilder()
                        .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                        .Build(),
                    stoppingToken
                );
            }
        }
    }
}