IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

var unity = builder.AddUnityProject("game", "..\\..\\AspireIntegration", customUnityInstallRoot: "E:\\Unity");

var container = builder.AddContainer("test", "docker/welcome-to-docker");

container.WaitFor(unity);

builder.Build().Run();