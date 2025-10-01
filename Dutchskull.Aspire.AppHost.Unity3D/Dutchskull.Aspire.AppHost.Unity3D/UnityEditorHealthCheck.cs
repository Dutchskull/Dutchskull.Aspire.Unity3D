using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Dutchskull.Aspire.Unity3D.Hosting;

public sealed class UnityEditorHealthCheck : IHealthCheck
{
    private readonly Uri _controlUrl;
    private readonly ILogger<UnityEditorHealthCheck> _log;
    private readonly IHttpClientFactory _httpFactory;

    public UnityEditorHealthCheck(ILogger<UnityEditorHealthCheck> log, IHttpClientFactory httpFactory, Uri controlUrl)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
        _controlUrl = controlUrl ?? throw new ArgumentNullException(nameof(controlUrl));
        _log.LogDebug("UnityHealthCheck constructed for {Url}", controlUrl);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        _log.LogDebug("UnityHealthCheck probing {Url} at {Time}", _controlUrl, DateTime.UtcNow);

        try
        {
            HttpClient client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);

            Uri url = new(_controlUrl, "editor-health");
            HttpResponseMessage response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _log.LogDebug("Unity control healthy: {Status}", response.StatusCode);
                return HealthCheckResult.Healthy("Unity control OK");
            }

            _log.LogWarning("Unity control unhealthy: {Status}", response.StatusCode);
            return HealthCheckResult.Unhealthy($"Status {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "UnityHealthCheck failed contacting control endpoint");
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}