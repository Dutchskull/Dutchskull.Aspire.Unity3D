using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Dutchskull.Aspire.Unity3D.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130
namespace Aspire.Hosting;

public static class UnityAspireExtensions
{
    public static IResourceBuilder<UnityProjectResource> AddUnityProject(
        this IDistributedApplicationBuilder builder,
        string name,
        string projectPath,
        string url = "http://127.0.0.1",
        int port = 54021,
        string? customUnityInstallRoot = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

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

        builder.Services.AddHealthChecks()
            .AddTypeActivatedCheck<UnityHealthCheck>(
                healthCheckKey,
                args: [controlUrl]
            );

        IResourceBuilder<UnityProjectResource> unityBuilder = builder
            .AddResource(unityResource)
            .ExcludeFromManifest()
            .WithHealthCheck(healthCheckKey);

        UnityProcessManager processManager = new();
        UnityControlClient controlClient = new();

        unityBuilder.OnInitializeResource(async (resource, initEvent, cancellationToken) =>
        {
            ILogger log = initEvent.Logger;
            IDistributedApplicationEventing events = initEvent.Eventing;
            ResourceNotificationService notifications = initEvent.Notifications;
            IServiceProvider services = initEvent.Services;

            using (log.BeginScope("UnityProject:{ResourceName}", resource.Name))
            {
                await events.PublishAsync(new BeforeResourceStartedEvent(resource, services), cancellationToken);

                log.LogInformation("Initializing Unity project resource for {ProjectPath}", unityResource.ProjectPath);

                int existingPid = UnityProcessManager.FindEditorPidForProjectAsync(unityResource.ProjectPath);

                bool unityNotStarted = existingPid == -1;
                if (unityNotStarted)
                {
                    processManager.StartEditor(unityResource.UnityExePath, unityResource.ProjectPath);
                }

                await notifications.PublishUpdateAsync(resource, s => s with
                {
                    StartTimeStamp = DateTime.UtcNow,
                    State = KnownResourceStates.Starting
                });
            }
        });

        unityBuilder.OnResourceReady(async (resource, resourceReadyEvent, cancellationToken) =>
        {
            ILogger log = resourceReadyEvent.Services.GetRequiredService<ILogger>();
            ResourceNotificationService notifications = resourceReadyEvent.Services.GetRequiredService<ResourceNotificationService>();

            using (log.BeginScope("UnityProject:{ResourceName}", resource.Name))
            {
                try
                {
                    bool runSucceeded = await controlClient.StartProjectAsync(unityResource.ControlUrl, cancellationToken);

                    if (runSucceeded)
                    {
                        await notifications.PublishUpdateAsync(resource, s => s with
                        {
                            StartTimeStamp = DateTime.UtcNow,
                            State = KnownResourceStates.Running,
                            Urls = s.Urls.Add(new UrlSnapshot(unityResource.ControlUrl.ToString(), unityResource.ControlUrl.ToString(), true))
                        });
                    }
                    else
                    {
                        await notifications.PublishUpdateAsync(resource, s => s with
                        {
                            StartTimeStamp = DateTime.UtcNow,
                            State = "StartedButRunFailed",
                        });
                    }
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Error while contacting existing Unity control endpoint.");
                    await notifications.PublishUpdateAsync(resource, s => s with
                    {
                        StartTimeStamp = DateTime.UtcNow,
                        State = "DetectedProcessButControlError"
                    });
                }
            }
        });

        Console.CancelKeyPress += async (_, ea) => await StopUnityAsync(unityResource, controlClient);

        AppDomain.CurrentDomain.ProcessExit += async (_, _) => await StopUnityAsync(unityResource, controlClient);

        return unityBuilder;
    }

    private static async Task StopUnityAsync(UnityProjectResource unityResource, UnityControlClient unityControlClient) =>
        await unityControlClient.StopProjectAsync(unityResource.ControlUrl).ConfigureAwait(false);
}
