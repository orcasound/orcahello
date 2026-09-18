using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace NotificationSystem.ServiceDefaults;

/// <summary>
/// Shared "service defaults" for OrcaHello .NET services: a single call that turns on
/// OpenTelemetry logging, metrics and tracing and fans the signals out to whichever
/// backends the environment is configured for:
///
/// <list type="bullet">
///   <item><b>OTLP</b> — when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set (the Aspire app
///     host injects this automatically), signals go to the Aspire dashboard locally, or to
///     any OTLP collector in a deployed environment.</item>
///   <item><b>Azure Monitor / Application Insights</b> — when
///     <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> is set, signals are exported straight to
///     App Insights via the Azure Monitor OpenTelemetry exporter.</item>
/// </list>
///
/// Both can be active at once. Pairing this with <c>"telemetryMode": "OpenTelemetry"</c> in
/// host.json makes the Functions host emit through the same OpenTelemetry pipeline instead
/// of its legacy Application Insights SDK, so host and worker signals are correlated and not
/// double-counted.
///
/// The canonical Aspire ServiceDefaults template extends <c>IHostApplicationBuilder</c>. The
/// NotificationSystem Functions app is an isolated worker built with the classic
/// <see cref="IHostBuilder"/> (<c>new HostBuilder().ConfigureFunctionsWorkerDefaults()</c>),
/// so the defaults are exposed as an <see cref="IHostBuilder"/> extension here instead.
/// </summary>
public static class Extensions
{
    private const string OtlpEndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string AzureMonitorConnectionVariable = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    /// <summary>
    /// Adds OpenTelemetry logging, metrics and tracing, exporting to OTLP and/or Azure
    /// Monitor depending on which of <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> /
    /// <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> are present. Instrumentation is still
    /// collected when neither is set — it is simply not exported.
    /// </summary>
    public static IHostBuilder AddServiceDefaults(this IHostBuilder builder)
    {
        builder.ConfigureLogging((context, logging) =>
        {
            var azureMonitorConnectionString = context.Configuration[AzureMonitorConnectionVariable];

            logging.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;

                if (HasOtlpEndpoint(context.Configuration))
                {
                    options.AddOtlpExporter();
                }

                if (!string.IsNullOrWhiteSpace(azureMonitorConnectionString))
                {
                    options.AddAzureMonitorLogExporter(o => o.ConnectionString = azureMonitorConnectionString);
                }
            });
        });

        builder.ConfigureServices((context, services) =>
        {
            var configuration = context.Configuration;
            var azureMonitorConnectionString = configuration[AzureMonitorConnectionVariable];
            var hasOtlp = HasOtlpEndpoint(configuration);
            var hasAzureMonitor = !string.IsNullOrWhiteSpace(azureMonitorConnectionString);

            services.AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation();

                    if (hasOtlp)
                    {
                        metrics.AddOtlpExporter();
                    }

                    if (hasAzureMonitor)
                    {
                        metrics.AddAzureMonitorMetricExporter(o => o.ConnectionString = azureMonitorConnectionString);
                    }
                })
                .WithTracing(tracing =>
                {
                    tracing.AddHttpClientInstrumentation();

                    if (hasOtlp)
                    {
                        tracing.AddOtlpExporter();
                    }

                    if (hasAzureMonitor)
                    {
                        tracing.AddAzureMonitorTraceExporter(o => o.ConnectionString = azureMonitorConnectionString);
                    }
                });
        });

        return builder;
    }

    private static bool HasOtlpEndpoint(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration[OtlpEndpointVariable]);
}
