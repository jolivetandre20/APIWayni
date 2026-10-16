using Wayni.Deudores.Infrastructure;
using Wayni.Deudores.Processor;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);

builder.Services.Configure<ProcessingOptions>(builder.Configuration.GetSection("Processing"));
builder.Services.AddWayniPersistence(builder.Configuration);
builder.Services.AddWayniAws(builder.Configuration);
builder.Services.AddWayniNotifications(builder.Configuration);
builder.Services.AddScoped<ImportProcessor>();
builder.Services.AddHostedService<ImportWorker>();

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();

