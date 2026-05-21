using System.Diagnostics;
using System.Net.Http.Json;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("orders", client =>
{
    client.BaseAddress = new Uri("http://localhost:5201");
});

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("ProductService"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
                options.Endpoint = new Uri("http://localhost:4318/v1/traces");
            });
    });

var app = builder.Build();

app.MapGet("/products/{id:int}/with-orders", async (
    int id,
    IHttpClientFactory httpClientFactory) =>
{
    var client = httpClientFactory.CreateClient("orders");
    var orders = await client.GetFromJsonAsync<OrderDto[]>($"/orders/by-product/{id}");

    return Results.Ok(new
    {
        Service = "ProductService",
        Product = new { Id = id, Name = $"Product-{id}", Price = 100 + id },
        Orders = orders ?? Array.Empty<OrderDto>(),
        TraceId = Activity.Current?.TraceId.ToString()
    });
});

app.MapGet("/products/{id:int}", (int id) =>
{
    return Results.Ok(new
    {
        Service = "ProductService",
        Product = new { Id = id, Name = $"Product-{id}", Price = 100 + id },
        TraceId = Activity.Current?.TraceId.ToString()
    });
});

app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();

public sealed record OrderDto(int Id, int ProductId, int Count, decimal Amount);
