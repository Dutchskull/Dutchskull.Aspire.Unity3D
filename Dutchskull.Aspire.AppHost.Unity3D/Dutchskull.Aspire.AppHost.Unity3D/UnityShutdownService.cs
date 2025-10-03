using Dutchskull.Aspire.Unity3D.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130
namespace Aspire.Hosting;

public class UnityShutdownService : IHostedService
{
    private readonly ILogger<UnityShutdownService> _logger;
    private readonly UnityProjectResource _resource;
    private readonly UnityControlClient _controlClient;

    public UnityShutdownService(
        IHostApplicationLifetime lifetime,
        ILogger<UnityShutdownService> logger,
        UnityProjectResource unity,
        UnityControlClient unityControlClient)
    {
        _logger = logger;
        _resource = unity;
        _controlClient = unityControlClient;

        lifetime.ApplicationStopped.Register(() =>
        {
            _ = StopAsync(CancellationToken.None);
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            _ = StopAsync(CancellationToken.None);
        });
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Host stopping — running Unity shutdown.");
        try
        {
            await _controlClient.StopProjectAsync(_resource.ControlUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Unity shutdown canceled.");
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error during Unity shutdown.");
        }
    }
}
