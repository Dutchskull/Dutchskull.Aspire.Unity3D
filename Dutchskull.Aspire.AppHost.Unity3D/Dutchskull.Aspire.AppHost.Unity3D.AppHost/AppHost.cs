using Projects;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Dutchskull_Aspire_Apphost_Unity3D_Api>("api");

var unity = builder
    .AddUnityProject("game", "..\\..\\AspireIntegration", 1, customUnityInstallRoot: "E:\\Unity")
    .WithReference(api)
    .WithEnvironment("Test", "Value")
    .WaitFor(api);

var container = builder.AddContainer("test", "docker/welcome-to-docker");

container.WaitFor(unity);

builder.Build().Run();