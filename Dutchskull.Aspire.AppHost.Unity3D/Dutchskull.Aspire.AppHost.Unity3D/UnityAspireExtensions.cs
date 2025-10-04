using Aspire.Hosting.ApplicationModel;

using Dutchskull.Aspire.Unity3D.Hosting;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using System.Diagnostics;

#pragma warning disable IDE0130

namespace Aspire.Hosting;

public static class UnityAspireExtensions
{
    public static IResourceBuilder<UnityProjectResource> AddUnityProject(
        this IDistributedApplicationBuilder builder,
        string name,
        string projectPath) =>
            AddUnityProject(builder, name, projectPath, -1);

    public static IResourceBuilder<UnityProjectResource> AddUnityProject(
        this IDistributedApplicationBuilder builder,
        string name,
        string projectPath,
        int sceneIndex,
        string url = "http://127.0.0.1",
        int port = 54021,
        string? customUnityInstallRoot = null)
    {
        return builder.AddUnityProject(name, projectPath, sceneIndex.ToString(), url, port, customUnityInstallRoot);
    }

    public static IResourceBuilder<UnityProjectResource> AddUnityProject(
        this IDistributedApplicationBuilder builder,
        string name,
        string projectPath,
        string sceneName,
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

        string healthCheckEditorKey = $"{name}_editor_check";
        string healthCheckPlaymodeKey = $"{name}_playmode_check";

        builder.Services.AddHttpClient();

        builder
            .Services.AddHealthChecks()
            .AddTypeActivatedCheck<UnityEditorHealthCheck>(
                healthCheckEditorKey,
                args: [controlUrl])
            .AddTypeActivatedCheck<UnityPlaymodeHealthCheck>(
                healthCheckPlaymodeKey,
                args: [controlUrl]);

        IResourceBuilder<UnityProjectResource> unityBuilder = builder
            .AddResource(unityResource)
            .ExcludeFromManifest()
            .WithHealthCheck(healthCheckEditorKey)
            .WithHealthCheck(healthCheckPlaymodeKey);

        UnityProcessManager processManager = new();
        UnityControlClient controlClient = new();

        async Task StartUnityAsync(
            UnityProjectResource resource,
            ResourceNotificationService notifications,
            ILogger log,
            IServiceProvider services,
            CancellationToken cancellationToken)
        {
            using IDisposable? scope = log.BeginScope("UnityProject:{ResourceName}", resource.Name);
            await notifications
                .PublishUpdateAsync(resource, s => s with { State = KnownResourceStates.Starting })
                .ConfigureAwait(false);

            log.LogInformation(
                "Initializing Unity project resource for {ProjectPath}",
                unityResource.ProjectPath);

            Process? existingUnityProcess =
                UnityProcessManager.FindEditorProcessForProjectAsync(unityResource.ProjectPath);

            bool unityNotStarted = existingUnityProcess is null;

            if (unityNotStarted)
            {
                existingUnityProcess = processManager.StartEditor(
                    unityResource.UnityExePath,
                    unityResource.ProjectPath);
            }

            existingUnityProcess!.EnableRaisingEvents = true;
            existingUnityProcess.Exited += async (sender, e) =>
            {
                await notifications
                    .PublishUpdateAsync(
                        resource,
                        s => s with
                        {
                            StartTimeStamp = DateTime.UtcNow,
                            State = KnownResourceStates.Exited,
                            ResourceType = "unity",
                            Urls = [],
                        })
                    .ConfigureAwait(false);
            };

            await notifications
                .PublishUpdateAsync(
                    resource,
                    s => s with
                    {
                        StartTimeStamp = DateTime.UtcNow,
                        State = KnownResourceStates.Starting,
                    })
                .ConfigureAwait(false);

            TimeSpan pollInterval = TimeSpan.FromSeconds(10);
            TimeSpan overallTimeout = TimeSpan.FromMinutes(5);
            DateTime start = DateTime.UtcNow;

            HealthCheckService healthCheck = services.GetRequiredService<HealthCheckService>();

            while (!cancellationToken.IsCancellationRequested && (DateTime.UtcNow - start) < overallTimeout)
            {
                HealthReport health =
                    await healthCheck.CheckHealthAsync(cancellationToken).ConfigureAwait(false);

                if (health.Entries[healthCheckEditorKey].Status == HealthStatus.Healthy)
                {
                    break;
                }

                await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                bool runSucceeded = await controlClient
                    .StartProjectAsync(unityResource.ControlUrl, sceneName, cancellationToken)
                    .ConfigureAwait(false);

                if (runSucceeded)
                {
                    await notifications
                        .PublishUpdateAsync(
                            resource,
                            s => s with
                            {
                                StartTimeStamp = DateTime.UtcNow,
                                State = KnownResourceStates.Running,
                                Urls = [new UrlSnapshot(
                                        unityResource.ControlUrl.ToString(),
                                        unityResource.ControlUrl.ToString(),
                                        false)]
                            })
                        .ConfigureAwait(false);
                }
                else
                {
                    await notifications
                        .PublishUpdateAsync(
                            resource,
                            s => s with
                            {
                                StartTimeStamp = DateTime.UtcNow,
                                State = "StartedButRunFailed",
                            })
                        .ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Error while contacting existing Unity control endpoint.");
                await notifications
                    .PublishUpdateAsync(
                        resource,
                        s => s with
                        {
                            StartTimeStamp = DateTime.UtcNow,
                            State = "DetectedProcessButControlError"
                        })
                    .ConfigureAwait(false);
            }
        }

        unityBuilder.OnInitializeResource(async (resource, initEvent, cancellationToken) =>
        {
            await initEvent
                .Eventing.PublishAsync(
                    new BeforeResourceStartedEvent(resource, initEvent.Services),
                    cancellationToken)
                .ConfigureAwait(false);

            await StartUnityAsync(
                    resource,
                    initEvent.Notifications,
                    initEvent.Logger,
                    initEvent.Services,
                    cancellationToken)
                .ConfigureAwait(false);
        });

        unityBuilder.WithCommand(
            "resource-start",
            "Start Unity",
            async context =>
            {
                ResourceNotificationService notifications = context.ServiceProvider.GetRequiredService<ResourceNotificationService>();
                ILogger<UnityProjectResource> logger = context.ServiceProvider.GetRequiredService<ILogger<UnityProjectResource>>();

                await StartUnityAsync(
                        unityResource,
                        notifications,
                        logger,
                        context.ServiceProvider,
                        context.CancellationToken)
                    .ConfigureAwait(true);

                return new ExecuteCommandResult { Success = true };
            },
            new CommandOptions
            {
                IconName = "Play",
                IconVariant = IconVariant.Filled,
                UpdateState = context => OnUpdateResourceState(context, HealthStatus.Unhealthy,
                [
                    KnownResourceStates.Exited!,
                    KnownResourceStates.FailedToStart!,
                    KnownResourceStates.RuntimeUnhealthy!,
                    KnownResourceStates.Finished!
                ]),
                IsHighlighted = true,
            });

        unityBuilder.WithCommand(
            "resource-stop",
            "Stop Unity",
            async context =>
            {
                ResourceNotificationService? notificationService = context.ServiceProvider.GetRequiredService<ResourceNotificationService>();

                notificationService?.PublishUpdateAsync(unityResource, s => s with { State = KnownResourceStates.Stopping });
                await StopUnityAsync(unityResource, controlClient).ConfigureAwait(true);
                notificationService?.PublishUpdateAsync(unityResource, s => s with { State = KnownResourceStates.Finished });

                return new ExecuteCommandResult { Success = true };
            },
            new CommandOptions
            {
                IconName = "Stop",
                IconVariant = IconVariant.Filled,
                UpdateState = context => OnUpdateResourceState(context, HealthStatus.Healthy),
                IsHighlighted = true,
            });

        Console.CancelKeyPress += async (_, ea) =>
            await StopUnityAsync(unityResource, controlClient).ConfigureAwait(false);

        AppDomain.CurrentDomain.ProcessExit += async (_, _) =>
            await StopUnityAsync(unityResource, controlClient).ConfigureAwait(false);

        return unityBuilder;
    }

    private static ResourceCommandState OnUpdateResourceState(
        UpdateCommandStateContext context,
        HealthStatus? visibleHealthStatus,
        ResourceStateSnapshot[]? showForState = null)
    {
        return context.ResourceSnapshot.HealthStatus == visibleHealthStatus ||
               (showForState is not null && showForState.Contains(context.ResourceSnapshot.State))
            ? ResourceCommandState.Enabled
            : ResourceCommandState.Hidden;
    }

    private static async Task StopUnityAsync(
        UnityProjectResource unityResource,
        UnityControlClient unityControlClient) =>
        await unityControlClient.StopProjectAsync(unityResource.ControlUrl).ConfigureAwait(false);
}