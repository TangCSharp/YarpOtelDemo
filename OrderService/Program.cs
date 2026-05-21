using System.Diagnostics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("OrderService"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
                options.Endpoint = new Uri("http://localhost:4318/v1/traces");
            });
    });

var app = builder.Build();

app.MapGet("/orders/by-product/{productId:int}", async (int productId) =>
{
    await Task.Delay(80);

    return Results.Ok(new[]
    {
        new OrderDto(1001, productId, 2, 398),
        new OrderDto(1002, productId, 1, 199)
    });
});

app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();

public sealed record OrderDto(int Id, int ProductId, int Count, decimal Amount);
