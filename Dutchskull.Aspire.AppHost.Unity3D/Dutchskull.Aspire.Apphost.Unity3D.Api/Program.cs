using Dutchskull.Aspire.Unity3D.ServiceDefaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

WebApplication app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
