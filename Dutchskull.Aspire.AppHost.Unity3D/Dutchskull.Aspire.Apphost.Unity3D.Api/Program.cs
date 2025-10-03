using Dutchskull.Aspire.Unity3D.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
