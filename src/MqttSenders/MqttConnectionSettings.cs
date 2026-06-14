using Microsoft.Extensions.Configuration;

namespace MqttSenders
{
    public sealed class MqttConnectionSettings
    {
        public string Host { get; init; } = "localhost";

        public int Port { get; init; } = 1883;

        public bool UseTls { get; init; }

        public string? CaCertificatePath { get; init; }

        public string? ClientCertificatePath { get; init; }

        public string? ClientKeyPath { get; init; }

        public static MqttConnectionSettings FromConfiguration(IConfigurationSection section)
        {
            return new MqttConnectionSettings
            {
                Host = section.GetValue<string>("Host") ?? "localhost",
                Port = section.GetValue<int?>("Port") ?? 1883,
                UseTls = section.GetValue<bool>("UseTls"),
                CaCertificatePath = section.GetValue<string>("CaCertificatePath"),
                ClientCertificatePath = section.GetValue<string>("ClientCertificatePath"),
                ClientKeyPath = section.GetValue<string>("ClientKeyPath"),
            };
        }
    }
}