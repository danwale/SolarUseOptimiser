using System.Runtime.Loader;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Serilog;

using SolarUseOptimiser.Models.Configuration;

namespace SolarUseOptimiser
{
    public class Program
    {
        private RoutingSettings Routing { get;set; }
        private static ServiceProvider ServiceProvider { get; set; }

        public static void Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Share the cancellation token source
            var cts = new CancellationTokenSource();

            // Initialse the poller passing it the Cancellation Token Source
            var poller = ServiceProvider.GetRequiredService<PeriodicPoller>()
                                        .InitialiseAsync(cts).GetAwaiter().GetResult();

            // Start the poller
            poller.Start();

            // Wait until the app unloads or is cancelled
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();

            // Stop the poller as the service is stopping
            poller.Stop();

            Log.Logger.Information("Solar Use Optimiser has exited.");
        }

        /// <summary>
        /// <c>WhenCancelled</c> - Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary
        /// <c>ConfigureServices</c> - Sets up the dependency injection of services
        /// </summary>
        private static void ConfigureServices(ServiceCollection serviceCollection)
        {
            string configPath = Environment.GetEnvironmentVariable("ConfigPath");
            if (string.IsNullOrWhiteSpace(configPath))
            {
                configPath = "/etc/solaruseoptimiser";
            }
            Console.WriteLine("Reading appsettings.json from: {0}", configPath);

            Console.WriteLine("ConfigureServices called");
            IConfiguration configuration = new ConfigurationBuilder()
                                                    .SetBasePath(configPath)
                                                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                                                    .AddEnvironmentVariables()
                                                    .Build();
            serviceCollection.AddSingleton(configuration);

            var routing = configuration.GetSection(Constants.ConfigSections.ROUTING_SECTION).Get<RoutingSettings>();
            if (routing == null)
            {
                routing = new RoutingSettings();
            }
            if (routing.Target.Equals(Constants.Routes.Targets.ROUTE_TARGET_CHARGEHQ, StringComparison.CurrentCultureIgnoreCase))
            {
                serviceCollection.AddSingleton<IDataTarget, ChargeHQSender>(); // Send to ChargeHQ PushAPI
            }
            if (routing.DataSource.Equals(Constants.Routes.DataSources.ROUTE_DATASOURCE_HUAWEI, StringComparison.CurrentCultureIgnoreCase))
            {
                serviceCollection.AddSingleton<IDataSource, HuaweiProducer>(); // Pull from Huawei FusionSolar
            }
            else if (routing.DataSource.Equals(Constants.Routes.DataSources.ROUTE_DATASOURCE_GROWATT, StringComparison.CurrentCultureIgnoreCase))
            {
                serviceCollection.AddSingleton<IDataSource, GrowattProducer>(); // Pull from the Growatt API
            }
            else if (routing.DataSource.Equals(Constants.Routes.DataSources.ROUTE_DATASOURCE_IOTAWATT, StringComparison.CurrentCultureIgnoreCase))
            {
                serviceCollection.AddSingleton<IDataSource, IoTaWattProducer>(); // Pull from the IoTaWatt Power Sensor API
            }
            serviceCollection.AddSingleton<PeriodicPoller>();

            // Configure Logger
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            serviceCollection.AddLogging((builder) => {
                builder.AddSerilog();
            });
            Log.Logger.Debug("Configured Logger");
        }
    }
}