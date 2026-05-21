# YarpOtelDemo

`YarpOtelDemo` 是一个演示 `YARP + OpenTelemetry + Jaeger` 链路追踪的 .NET 示例项目。

项目通过 `YARP Gateway` 代理商品服务，请求进入 `ProductService` 后，再通过 `HttpClient` 调用 `OrderService`，最终在 Jaeger 中看到完整链路。

```text
Client
  |
  v
Gateway
  |
  v
ProductService
  |
  v
OrderService
```

## 项目结构

```text
YarpOtelDemo
├── Gateway
├── ProductService
├── OrderService
└── YarpOtelDemo.sln
```

## 端口说明

| 服务 | 地址 |
| --- | --- |
| Jaeger UI | `http://localhost:16686` |
| Jaeger OTLP HTTP | `http://localhost:4318/v1/traces` |
| Jaeger OTLP gRPC | `http://localhost:4317` |
| Gateway | `http://localhost:5000` |
| ProductService | `http://localhost:5101` |
| OrderService | `http://localhost:5201` |

当前示例代码使用 `4318`，也就是 OTLP HTTP：

```csharp
options.Protocol = OtlpExportProtocol.HttpProtobuf;
options.Endpoint = new Uri("http://localhost:4318/v1/traces");
```

## 启动 Jaeger

```bash
docker run --rm --name jaeger \
  -e COLLECTOR_OTLP_ENABLED=true \
  -p 16686:16686 \
  -p 4317:4317 \
  -p 4318:4318 \
  jaegertracing/all-in-one:1.57
```

打开 Jaeger UI：

```text
http://localhost:16686
```

## 启动服务

在项目根目录执行。

启动 `OrderService`：

```bash
dotnet run --project OrderService/OrderService.csproj --urls http://localhost:5201
```

启动 `ProductService`：

```bash
dotnet run --project ProductService/ProductService.csproj --urls http://localhost:5101
```

启动 `Gateway`：

```bash
dotnet run --project Gateway/Gateway.csproj
```

## 测试请求

访问网关自身接口：

```bash
curl -i http://localhost:5000/gateway/ping
```

访问商品接口：

```bash
curl -i http://localhost:5000/api/products/1
```

访问完整链路接口：

```bash
curl -i http://localhost:5000/api/products/1/with-orders
```

完整链路接口会经过：

```text
Gateway
  -> ProductService
    -> OrderService
```

响应头里会返回 `X-Trace-Id`：

```text
X-Trace-Id: 72ea967cb713bc526d495299171a1b05
```

响应体里也会包含当前 TraceId。

## 查看 Jaeger

打开：

```text
http://localhost:16686/search
```

选择服务：

```text
Gateway
ProductService
OrderService
```

点击 `Find Traces`。

也可以用 API 查询：

```bash
curl http://localhost:16686/api/services
```

查询最近 5 分钟的 Gateway 链路：

```bash
curl "http://localhost:16686/api/traces?service=Gateway&lookback=5m&limit=20"
```

根据 TraceId 查询：

```bash
curl http://localhost:16686/api/traces/<trace-id>
```

## 关键配置

`Gateway` 需要额外监听 `Yarp.ReverseProxy`：

```csharp
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
```

`ProductService` 需要 `AddHttpClientInstrumentation()`，因为它会调用 `OrderService`：

```csharp
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
```

`OrderService` 只需要采集 ASP.NET Core 请求：

```csharp
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
```

## 常见问题

### Jaeger UI 里没有 Gateway / ProductService / OrderService

先确认已经请求过接口：

```bash
curl -i http://localhost:5000/api/products/1/with-orders
```

再确认 Jaeger 服务列表：

```bash
curl http://localhost:16686/api/services
```

如果仍然没有业务服务，检查三个项目是否都配置了：

```csharp
options.Protocol = OtlpExportProtocol.HttpProtobuf;
options.Endpoint = new Uri("http://localhost:4318/v1/traces");
```

### 4318 不能只写 localhost:4318

错误写法：

```csharp
options.Endpoint = new Uri("http://localhost:4318");
```

正确写法：

```csharp
options.Protocol = OtlpExportProtocol.HttpProtobuf;
options.Endpoint = new Uri("http://localhost:4318/v1/traces");
```

`4318` 是 OTLP HTTP，需要使用 `HttpProtobuf`，并且路径要带 `/v1/traces`。

### 4317 是否需要 /v1/traces

不需要。

`4317` 是 OTLP gRPC：

```csharp
options.Endpoint = new Uri("http://localhost:4317");
```

`4318` 是 OTLP HTTP：

```csharp
options.Protocol = OtlpExportProtocol.HttpProtobuf;
options.Endpoint = new Uri("http://localhost:4318/v1/traces");
```

当前示例主线使用 `4318`。

### Jaeger UI 显示旧 Trace

如果浏览器地址栏里带着：

```text
start=...
end=...
```

可能正在查询旧时间段。

直接打开干净地址：

```text
http://localhost:16686/search
```

重新选择 `Service` 和 `Last 5 Minutes`，再点击 `Find Traces`。

## 清理

停止 Jaeger：

```bash
docker stop jaeger
```

停止三个 .NET 服务，在对应终端按 `Ctrl+C`。
