using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vicgital.Core.Logging.Serilog.Configuration;
using Vicgital.Core.Logging.Serilog.Extensions;
using Vicgital.Grpc.Interceptors;

namespace Vicgital.Grpc
{
    public static class VicgitalGrpcService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.AspNetCore.Builder.WebApplicationBuilder"/> class with preconfigured defaults for Vicgital Grpc Services.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The <see cref="Microsoft.AspNetCore.Builder.WebApplicationBuilder"/>.</returns>
        public static WebApplicationBuilder CreateWebApplicationBuilder(
            string[] args, IConfiguration? config = null)
        {
            // Configuration
            config ??= Core.Configuration.ConfigurationBuilder.BuildConfiguration();

            var builder = WebApplication.CreateBuilder(args);

            // WebHost
            var port = config.GetValue("Grpc:Port", 50051);
            var host = config.GetValue<string?>("Grpc:Host", null);

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.AddServerHeader = false;
                if (string.IsNullOrWhiteSpace(host))
                    options.ListenAnyIP(port, o => o.Protocols = HttpProtocols.Http2);
                else
                    options.Listen(IPAddress.Parse(host), port, o => o.Protocols = HttpProtocols.Http2);
            });

            builder.Configuration.AddConfiguration(config);

            // Logging
            builder.Logging.ClearProviders();
            if (config.GetSection("Serilog") == null)
                builder.Services.AddSerilogLogging(LoggerConfigurationBuilder.BuildFromConfiguration(config).CreateLogger());
            else
                builder.Services.AddSerilogLogging(LoggerConfigurationBuilder.BuildDefault(Serilog.Events.LogEventLevel.Warning).CreateLogger());

            // Interceptors
            builder.Services.AddGrpc(o =>
            {
                o.Interceptors.Add<ExceptionHandlerInterceptor>();
                o.Interceptors.Add<ValidationInterceptor>();
            });

            // Reflection
            builder.Services.AddGrpcReflection();

            // Health checks
            builder.Services.AddGrpcHealthChecks();

            return builder;

        }

        /// <summary>
        /// Maps Vicgital Grpc infrastructure endpoints (gRPC server reflection and health checks) onto the app.
        /// Reflection is exposed automatically in Development, and otherwise only when
        /// <c>Grpc:EnableReflection</c> is explicitly set to <c>true</c> in configuration, since it lets
        /// any client enumerate the full service/message schema. Health checks are always mapped via the
        /// standard grpc.health.v1.Health service, backed by whatever checks are registered through
        /// <see cref="Microsoft.Extensions.DependencyInjection.HealthCheckServiceCollectionExtensions.AddHealthChecks"/>.
        /// </summary>
        /// <param name="app">The <see cref="Microsoft.AspNetCore.Builder.WebApplication"/>.</param>
        /// <returns>The <see cref="Microsoft.AspNetCore.Builder.WebApplication"/>.</returns>
        public static WebApplication MapVicgitalGrpcEndpoints(this WebApplication app)
        {
            ArgumentNullException.ThrowIfNull(app);

            if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Grpc:EnableReflection", false))
            {
                app.MapGrpcReflectionService();
            }

            app.MapGrpcHealthChecksService();

            return app;
        }
    }
}
