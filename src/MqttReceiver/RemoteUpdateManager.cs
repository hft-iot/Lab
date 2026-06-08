using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MqttReceiver;

public sealed class RemoteUpdateManager(
    ILogger<RemoteUpdateManager> logger,
    IConfiguration configuration
) : BackgroundService
{
    private static readonly HttpClient HttpClient = new();
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILogger<RemoteUpdateManager> _logger = logger;
    private readonly string _deviceId =
        configuration.GetValue<string>("UpdateControllerId") ?? string.Empty;
    private readonly string _targetToken =
        configuration.GetValue<string>("UpdateControllerToken") ?? string.Empty;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_targetToken))
        {
            _logger.LogWarning(
                "Remote update manager disabled because UpdateControllerToken is empty."
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(_deviceId))
        {
            _logger.LogWarning(
                "Remote update manager disabled because UpdateControllerId is empty."
            );
            return;
        }

        var controllerUrl = $"http://hawkbit:8080/default/controller/v1/{_deviceId}";

        while (!stoppingToken.IsCancellationRequested)
        {
            await PollControllerAsync(controllerUrl, stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task PollControllerAsync(
        string controllerUrl,
        CancellationToken cancellationToken
    )
    {
        try

        {
            using var request = CreateRequest(controllerUrl);
            using var response = await HttpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Remote update controller responded with status code {StatusCode} for {Url}.",
                    (int)response.StatusCode,
                    controllerUrl
                );
                return;
            }

            var controllerResponse = await DeserializeAsync<ControllerResponse>(
                response,
                cancellationToken
            );
            var deploymentUrl = controllerResponse?.Links?.DeploymentBase?.Href;

            if (string.IsNullOrWhiteSpace(deploymentUrl))
            {
                return;
            }

            await LogDeploymentSummaryAsync(deploymentUrl, cancellationToken);
        }
        catch (Exception)
        {
            // do nothing
        }
    }

    private async Task LogDeploymentSummaryAsync(
        string deploymentUrl,
        CancellationToken cancellationToken
    )
    {
        using var request = CreateRequest(deploymentUrl);
        using var response = await HttpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Remote update deployment request responded with status code {StatusCode} for {Url}.",
                (int)response.StatusCode,
                deploymentUrl
            );
            return;
        }

        var deploymentResponse = await DeserializeAsync<DeploymentResponse>(
            response,
            cancellationToken
        );

        if (deploymentResponse is null)
        {
            _logger.LogWarning("Remote update deployment response could not be parsed.");
            return;
        }

        _logger.LogInformation(
            "Deployment available: {Summary}",
            BuildDeploymentSummary(deploymentResponse)
        );
    }

    private HttpRequestMessage CreateRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("TargetToken", _targetToken);
        return request;
    }

    private static async Task<T?> DeserializeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        await using var responseStream = await response.Content.ReadAsStreamAsync(
            cancellationToken
        );
        return await JsonSerializer.DeserializeAsync<T>(
            responseStream,
            JsonSerializerOptions,
            cancellationToken
        );
    }

    private static string BuildDeploymentSummary(DeploymentResponse deploymentResponse)
    {
        var deployment = deploymentResponse.Deployment;
        if (deployment is null)
        {
            return $"Id={deploymentResponse.Id}, no deployment payload available.";
        }

        var builder = new StringBuilder();
        builder.Append($"Id={deploymentResponse.Id}");
        builder.Append($", download={deployment.Download}");
        builder.Append($", update={deployment.Update}");

        foreach (var chunk in deployment.Chunks)
        {
            builder.Append($", chunk[{chunk.Part}] {chunk.Name} v{chunk.Version}");

            foreach (var artifact in chunk.Artifacts)
            {
                builder.Append(
                    $" -> {artifact.Filename} ({artifact.Size} bytes, sha256={artifact.Hashes?.Sha256})"
                );
            }
        }

        return builder.ToString();
    }

    private sealed class ControllerResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("_links")]
        public ControllerLinks? Links { get; init; }
    }

    private sealed class ControllerLinks
    {
        public LinkValue? DeploymentBase { get; init; }
    }

    private sealed class LinkValue
    {
        public string? Href { get; init; }
    }

    private sealed class DeploymentResponse
    {
        public string? Id { get; init; }

        public DeploymentPayload? Deployment { get; init; }
    }

    private sealed class DeploymentPayload
    {
        public string? Download { get; init; }

        public string? Update { get; init; }

        public IReadOnlyList<DeploymentChunk> Chunks { get; init; } = [];
    }

    private sealed class DeploymentChunk
    {
        public string? Part { get; init; }

        public string? Version { get; init; }

        public string? Name { get; init; }

        public IReadOnlyList<DeploymentArtifact> Artifacts { get; init; } = [];
    }

    private sealed class DeploymentArtifact
    {
        public string? Filename { get; init; }

        public long Size { get; init; }

        public ArtifactHashes? Hashes { get; init; }
    }

    private sealed class ArtifactHashes
    {
        public string? Sha256 { get; init; }
    }
}
