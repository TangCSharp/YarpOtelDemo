using System.Diagnostics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("Gateway"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("Yarp.ReverseProxy")
            .AddOtlpExporter(options =>
            {
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
                options.Endpoint = new Uri("http://localhost:4318/v1/traces");
            });
    });

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var traceId = Activity.Current?.TraceId.ToString();

        if (!string.IsNullOrWhiteSpace(traceId))
        {
            context.Response.Headers["X-Trace-Id"] = traceId;
        }

        return Task.CompletedTask;
    });

    await next();
});

app.MapGet("/gateway/ping", () => Results.Ok(new
{
    Service = "Gateway",
    TraceId = Activity.Current?.TraceId.ToString(),
    Time = DateTimeOffset.Now
}));

app.MapReverseProxy();

app.Run();
