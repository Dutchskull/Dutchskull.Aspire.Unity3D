using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Dutchskull.Aspire.Unity3D.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading;

#pragma warning disable IDE0130
namespace Aspire.Hosting;

public static class UnityAspireExtensions
{
    public static IResourceBuilder<UnityProjectResource> AddUnityProject(
    this IDistributedApplicationBuilder builder,
    string name,
    string projectPath,
    int? sceneIndex = null,
    string url = "http://127.0.0.1",
    int port = 54021,
    string? customUnityInstallRoot = null) =>
        builder.AddUnityProject(name, projectPath, sceneIndex.ToString(), url, port, customUnityInstallRoot);

    public static IResourceBuilder<UnityProjectResource> AddUnityProject(
        this IDistributedApplicationBuilder builder,
        string name,
        string projectPath,
        string? sceneName = null,
        string url = "http://127.0.0.1",
        int port = 54021,
        string? customUnityInstallRoot = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        projectPath = Path.GetFullPath(projectPath);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name required", nameof(name));
        }

        Uri controlUrl = new($"{url.TrimEnd('/')}:{port}");
        string? unityVersion = UnityPathFinder.ReadUnityVersionFromProject(projectPath);

        if (string.IsNullOrWhiteSpace(unityVersion))
        {
            throw new InvalidOperationException($"Unity version not found for {projectPath}");
        }

        string? editorPath = UnityPathFinder.GetUnityEditorPathForProject(projectPath, unityVersion, customUnityInstallRoot);
        if (string.IsNullOrWhiteSpace(editorPath))
        {
            throw new InvalidOperationException($"Unity editor {unityVersion} not installed for {projectPath}");
        }

        UnityProjectResource unityResource = new(name, editorPath, projectPath, controlUrl);

        string healthCheckKey = $"{name}_check";

        builder.Services.AddHttpClient();

        builder.Services.AddHostedService(serviceProvider =>
            new UnityShutdownService(
                serviceProvider.GetRequiredService<IHostApplicationLifetime>(),
                serviceProvider.GetRequiredService<ILogger<UnityShutdownService>>(),
                unityResource,
                serviceProvider.GetRequiredService<UnityControlClient>())
        );

        builder.Services.AddSingleton<UnityProcessManager>();
        builder.Services.AddSingleton<UnityControlClient>();

        builder.Services.AddHealthChecks()
            .AddTypeActivatedCheck<UnityHealthCheck>(
                healthCheckKey,
                args: [controlUrl]
            );

        IResourceBuilder<UnityProjectResource> unityBuilder = builder
            .AddResource(unityResource)
            .ExcludeFromManifest()
            .WithHealthCheck(healthCheckKey);


        unityBuilder.OnInitializeResource(async (resource, initEvent, cancellationToken) =>
        {
            ILogger log = initEvent.Logger;
            IDistributedApplicationEventing events = initEvent.Eventing;
            ResourceNotificationService notifications = initEvent.Notifications;
            IServiceProvider services = initEvent.Services;

            UnityProcessManager processManager = services.GetRequiredService<UnityProcessManager>();
            UnityControlClient controlClient = services.GetRequiredService<UnityControlClient>();

            using (log.BeginScope("UnityProject:{ResourceName}", resource.Name))
            {
                await events.PublishAsync(new BeforeResourceStartedEvent(resource, services), cancellationToken).ConfigureAwait(false);

                log.LogInformation("Initializing Unity project resource for {ProjectPath}", unityResource.ProjectPath);

                Process? existingUnityProcess = UnityProcessManager.FindEditorProcessForProjectAsync(unityResource.ProjectPath);

                bool unityNotStarted = existingUnityProcess is null;

                if (unityNotStarted)
                {
                    existingUnityProcess = processManager.StartEditor(unityResource.UnityExePath, unityResource.ProjectPath);
                }

                existingUnityProcess!.Exited += async (sender, e) =>
                {
                    await notifications.PublishUpdateAsync(resource, s => s with
                    {
                        StartTimeStamp = DateTime.UtcNow,
                        State = KnownResourceStates.Exited,
                        Urls = [],
                    }).ConfigureAwait(false);
                };

                await notifications.PublishUpdateAsync(resource, s => s with
                {
                    StartTimeStamp = DateTime.UtcNow,
                    State = KnownResourceStates.Starting,
                }).ConfigureAwait(false);

                TimeSpan pollInterval = TimeSpan.FromSeconds(10);
                TimeSpan overallTimeout = TimeSpan.FromMinutes(5);
                DateTime start = DateTime.UtcNow;

                HealthCheckService healthCheck = services.GetRequiredService<HealthCheckService>();

                while (!cancellationToken.IsCancellationRequested && (DateTime.UtcNow - start) < overallTimeout)
                {
                    HealthReport health = await healthCheck.CheckHealthAsync(cancellationToken).ConfigureAwait(false);

                    if (health.Status == HealthStatus.Healthy)
                    {
                        break;
                    }

                    await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
                }

                try
                {
                    bool runSucceeded = await controlClient.StartProjectAsync(unityResource.ControlUrl, sceneName, cancellationToken).ConfigureAwait(false);

                    if (runSucceeded)
                    {
                        await notifications.PublishUpdateAsync(resource, s => s with
                        {
                            StartTimeStamp = DateTime.UtcNow,
                            State = KnownResourceStates.Running,
                            Urls = s.Urls.Add(new UrlSnapshot(unityResource.ControlUrl.ToString(), unityResource.ControlUrl.ToString(), false))
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        await notifications.PublishUpdateAsync(resource, s => s with
                        {
                            StartTimeStamp = DateTime.UtcNow,
                            State = "StartedButRunFailed",
                        }).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Error while contacting existing Unity control endpoint.");
                    await notifications.PublishUpdateAsync(resource, s => s with
                    {
                        StartTimeStamp = DateTime.UtcNow,
                        State = "DetectedProcessButControlError"
                    }).ConfigureAwait(false);
                }
            }

            Console.CancelKeyPress += async (_, _) => await StopUnityAsync(unityResource, controlClient).ConfigureAwait(false);

            AppDomain.CurrentDomain.ProcessExit += async (_, _) => await StopUnityAsync(unityResource, controlClient).ConfigureAwait(false);
        });

        return unityBuilder;
    }

    private static async Task StopUnityAsync(UnityProjectResource unityResource, UnityControlClient unityControlClient) =>
        await unityControlClient.StopProjectAsync(unityResource.ControlUrl).ConfigureAwait(false);
}
