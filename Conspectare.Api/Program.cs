using Conspectare.Api.Configuration;
using Conspectare.Infrastructure.Migrations;
using FluentMigrator.Runner;

if (args.Contains("--migrate"))
{
    var migrateBuilder = WebApplication.CreateBuilder(args);
    var connectionString = migrateBuilder.Configuration.GetConnectionString("ConspectareDb");
    migrateBuilder.Services.AddFluentMigratorCore()
        .ConfigureRunner(rb => rb
            .AddMySql5()
            .WithGlobalConnectionString(connectionString)
            .ScanIn(typeof(Migration_001_Baseline).Assembly).For.Migrations())
        .AddLogging(lb => lb.AddFluentMigratorConsole());
    var migrateApp = migrateBuilder.Build();
    migrateApp.Services.GetRequiredService<IMigrationRunner>().MigrateUp();
    return;
}

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5100";
builder.WebHost.UseUrls($"http://+:{port}");

builder.Logging.AddJsonConsole();

Startup.ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

Startup.Configure(app, app.Environment);

app.Run();
