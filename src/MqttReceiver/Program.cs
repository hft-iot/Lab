using MqttReceiver;
using StreamProcessing;
using IoTHubConnector;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<MessageReceiver>();
builder.Services.AddHostedService<RemoteUpdateManager>();
builder.Services.AddStreamProcessing();
var iotHubConnectionString =
    builder.Configuration.GetValue<string>("IoTHubConnectionString")
    ?? throw new InvalidOperationException("IoTHubConnectionString not found in appsettings.json");
builder.Services.AddIotHubConnector(iotHubConnectionString);

var host = builder.Build();
await host.RunAsync();
