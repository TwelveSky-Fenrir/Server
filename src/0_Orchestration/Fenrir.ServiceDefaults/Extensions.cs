using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Fenrir.ServiceDefaults;

/// <summary>
/// Câblage transverse tiré <b>uniquement par les exécutables</b> (Login/Center/Game/Migrator) : OpenTelemetry
/// (logs + métriques + traces), service discovery, export OTLP conditionnel. Aspire = plan de contrôle ;
/// l'export OTLP sort <b>hors-bande</b> (jamais sur le port de jeu). Voir <c>Roadmap/FONDATIONS/03</c> et <c>08</c>.
/// </summary>
public static class Extensions
{
    extension<TBuilder>(TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        /// <summary>
        /// Point d'entrée unique des serveurs Fenrir : OpenTelemetry + service discovery.
        /// (Ex-<c>AddServiceDefaults</c> ; renommé <c>AddFenrirDefaults</c> — décision D-F1.)
        /// </summary>
        public TBuilder AddFenrirDefaults()
        {
            builder.ConfigureOpenTelemetry();

            builder.Services.AddServiceDiscovery();

            return builder;
        }

        /// <summary>OpenTelemetry : logs formatés + scopes, métriques runtime, trace source = nom d'application.</summary>
        public TBuilder ConfigureOpenTelemetry()
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });

            builder.Services.AddOpenTelemetry()
                .WithMetrics(metrics => { metrics.AddRuntimeInstrumentation(); })
                .WithTracing(tracing => { tracing.AddSource(builder.Environment.ApplicationName); });

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
