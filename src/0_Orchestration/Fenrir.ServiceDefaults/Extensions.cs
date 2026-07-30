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

            builder.Services.AddServiceDiscovery();

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

                    // Sans cette ligne, AUCUN instrument Fenrir n'est exporte : ZoneTickMetrics produit
                    // fenrir.zone.tick.stage.duration 3 fois par tick et par map, soit des milliers
                    // d'enregistrements par seconde integralement jetes. Le joker est necessaire parce que
                    // ZoneTickMetrics est `internal` : sa constante MeterName est inaccessible d'ici.
                    metrics.AddMeter("Fenrir.*");
                })
                .WithTracing(tracing =>
                {
                    // ApplicationName vaut "Fenrir.GameServer" alors que l'unique ActivitySource du depot
                    // s'appelle "Fenrir" : le recouvrement etait nul, aucune trace ne sortait.
                    tracing.AddSource("Fenrir.*");
                });

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
