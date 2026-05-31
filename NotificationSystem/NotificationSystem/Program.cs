using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotificationSystem;
using NotificationSystem.Models;
using NotificationSystem.Utilities;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration(config =>
    {
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddLogging();
        // Add DI services here
        services.AddSingleton(context.Configuration);
        services.AddHttpClient<OrcasiteHelper>();
        services.AddSingleton<PostToOrcasite>();
        services.AddSingleton<IEmailService, AwsEmailService>();
    })
    .Build();

host.Run();
