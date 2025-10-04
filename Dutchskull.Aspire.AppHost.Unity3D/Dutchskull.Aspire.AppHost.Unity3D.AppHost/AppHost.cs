using Dutchskull.Aspire.Unity3D.Hosting;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<UnityProjectResource> unity = builder.AddUnityProject("game", "..\\..\\AspireIntegration", 1, customUnityInstallRoot: "E:\\Unity");

IResourceBuilder<ContainerResource> container = builder.AddContainer("test", "docker/welcome-to-docker");

container.WaitFor(unity);

builder.Build().Run();