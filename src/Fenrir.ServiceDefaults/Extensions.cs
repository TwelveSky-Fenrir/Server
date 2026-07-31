using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Fenrir.ServiceDefaults;

public static class Extensions
{
    extension<TBuilder>(TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        public TBuilder AddFenrirDefaults()
        {
            builder.ConfigureOpenTelemetry();
            builder.ConfigureShutdown();

            return builder;
        }

        public TBuilder ConfigureShutdown()
        {
            builder.Services.Configure<HostOptions>(options =>
            {
                options.ShutdownTimeout = TimeSpan.FromSeconds(60);

            });

            return builder;
        }

        public TBuilder ConfigureOpenTelemetry()
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });

            builder.Services.AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics.AddRuntimeInstrumentation();

                    metrics.AddMeter("Fenrir.*");
                })
                .WithTracing(tracing => { tracing.AddSource("Fenrir.*"); });

            builder.AddOpenTelemetryExporters();

            return builder;
        }

        private TBuilder AddOpenTelemetryExporters()
        {
            var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

            if (useOtlpExporter) builder.Services.AddOpenTelemetry().UseOtlpExporter();

            return builder;
        }
    }
}
