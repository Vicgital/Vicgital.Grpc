# Vicgital.Grpc

Shared gRPC hosting and interceptor toolkit for Vicgital services — Kestrel/host configuration, cross-cutting interceptors, and common gRPC plumbing.

It gives every Vicgital gRPC service the same defaults out of the box:

- Kestrel configured for HTTP/2 with a configurable listen port/host
- Serilog logging wired up via `Vicgital.Core.Logging`
- Consistent exception → gRPC status mapping across unary and streaming calls
- Optional FluentValidation-based request validation
- gRPC server reflection (for tools like `grpcurl`/Postman)
- gRPC health checks (`grpc.health.v1.Health`) for orchestrator liveness/readiness probes

## Installation

The package is published to GitHub Packages. Add the source and credentials to `nuget.config` in your service's repo:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="github" value="https://nuget.pkg.github.com/vicgital/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="vicgital" />
      <add key="ClearTextPassword" value="%GIT_PACKAGES_READ_ONLY_PAT%" />
    </github>
  </packageSourceCredentials>
</configuration>
```

`GIT_PACKAGES_READ_ONLY_PAT` must be set in the environment (a PAT with `read:packages` scope) wherever you restore, including CI.

Then reference the package:

```xml
<PackageReference Include="Vicgital.Grpc" Version="1.0.0" />
```

## Quick start

```csharp
using Vicgital.Grpc;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var builder = VicgitalGrpcService.CreateWebApplicationBuilder(configuration, args);

var app = builder.Build();

app.MapGrpcService<MyGrpcService>();
app.MapVicgitalGrpcEndpoints(); // maps reflection (conditionally) and health checks

app.Run();
```

> **Note:** `CreateWebApplicationBuilder` already calls `AddGrpc()` and registers the Vicgital interceptors — don't call `builder.Services.AddGrpc()` again yourself. Just map your service(s) with `app.MapGrpcService<T>()` and call `app.MapVicgitalGrpcEndpoints()` once, after mapping your services.

## Configuration

Read from the `IConfiguration` you pass into `CreateWebApplicationBuilder`:

| Key                     | Default    | Description                                                                 |
|-------------------------|------------|-------------------------------------------------------------------------------|
| `Grpc:Port`              | `50051`    | Port Kestrel listens on.                                                     |
| `Grpc:Host`              | *(unset)*  | Specific IP to bind to. Unset binds all interfaces (`ListenAnyIP`).         |
| `Grpc:EnableReflection`  | `false`    | Forces gRPC server reflection on outside `Development`. See below.          |
| `Serilog`                | *(unset)*  | Standard Serilog configuration section. Falls back to a sensible default logger (Information level) if absent. |

Example `appsettings.json`:

```json
{
  "Grpc": {
    "Port": 50051,
    "Host": "0.0.0.0",
    "EnableReflection": false
  }
}
```

## Interceptors

Registered automatically, in this order, by `CreateWebApplicationBuilder`:

1. **`ExceptionHandlerInterceptor`** — wraps unary, client-streaming, server-streaming, and duplex-streaming calls. `RpcException`s pass through untouched; `ArgumentException` maps to `StatusCode.InvalidArgument`; anything else is logged and mapped to `StatusCode.Internal` (the original exception message is never leaked to the client).
2. **`ValidationInterceptor`** — runs inside the exception handler. For each request message, it looks up `IValidator<TRequest>` (FluentValidation) from DI. If none is registered for that message type, the request passes through unvalidated. If one is registered and validation fails, the call is rejected with `StatusCode.InvalidArgument` and all failure messages joined into one string. For client-streaming/duplex calls, each streamed message is validated as it arrives.

To validate a request type, just register a validator the normal FluentValidation way — no changes to `.proto` files needed:

```csharp
public class CreateWidgetRequestValidator : AbstractValidator<CreateWidgetRequest>
{
    public CreateWidgetRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
```

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<CreateWidgetRequestValidator>();
// or: builder.Services.AddScoped<IValidator<CreateWidgetRequest>, CreateWidgetRequestValidator>();
```

## Reflection

Server reflection (`grpc.reflection.v1alpha.ServerReflection`) is registered in DI and mapped by `app.MapVicgitalGrpcEndpoints()`:

- Always mapped in the `Development` environment.
- Otherwise only mapped if `Grpc:EnableReflection` is explicitly `true`, since reflection lets any client enumerate your full service/message schema.

## Health checks

`app.MapVicgitalGrpcEndpoints()` always maps the standard `grpc.health.v1.Health` service, backed by whatever checks you register the normal ASP.NET Core way:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");
```

Point your orchestrator's gRPC liveness/readiness probe (e.g. Kubernetes `grpc` probe) at the configured port; no separate HTTP health endpoint is needed.

## Publishing

Pushed to GitHub Packages by `.github/workflows/publish_package.yml` on pushes to `main`. Bump `<Version>` in `Vicgital.Grpc.csproj` before merging a release-worthy change.
