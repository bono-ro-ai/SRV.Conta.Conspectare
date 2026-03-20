using Conspectare.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5100";
builder.WebHost.UseUrls($"http://+:{port}");

builder.Logging.AddJsonConsole();

Startup.ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

Startup.Configure(app, app.Environment);

app.Run();
