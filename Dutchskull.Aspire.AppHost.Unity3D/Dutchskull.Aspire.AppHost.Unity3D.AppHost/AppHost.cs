using Dutchskull.Aspire.Unity3D.Hosting;
using Projects;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ProjectResource> api = builder.AddProject<Dutchskull_Aspire_Apphost_Unity3D_Api>("api");

IResourceBuilder<UnityProjectResource> unity = builder
    .AddUnityProject("game", "..\\..\\AspireIntegration", 1, customUnityInstallRoot: "E:\\Unity")
    .WithReference(api)
    .WithEnvironment("Test", "Value")
    .WaitFor(api);

IResourceBuilder<ContainerResource> container = builder.AddContainer("test", "docker/welcome-to-docker");

container.WaitFor(unity);

builder.Build().Run();