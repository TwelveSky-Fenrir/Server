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
                // Ce budget est PARTAGE par tous les StopAsync, pas alloue a chacun. GameServer en compte 41,
                // et le plus lent est le drain de GameConnectionHost : chaque connexion en vol fait jusqu'a
                // quatre allers-retours base (flush final, journal de deconnexion, teardown de session). Les
                // 30 s par defaut sont un budget muet — depasse, l'hote abandonne les flushes restants sans
                // que rien ne distingue « tout est ecrit » de « on a coupe au milieu ».
                options.ShutdownTimeout = TimeSpan.FromSeconds(60);

                // ServicesStopConcurrently reste FALSE (defaut) : l'arret sequentiel en ordre inverse
                // d'enregistrement est un contrat dont le depot depend — GameConnectionHost est enregistre
                // apres PositionWriteBehindHost precisement pour que le drain finisse avant que le flusher
                // partage ne coupe sa boucle.
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
