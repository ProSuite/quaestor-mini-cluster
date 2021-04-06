using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;

namespace Quaestor.MiniCluster.Guardian
{
	class Program
	{
		static async Task Main(string[] args)
		{
			// TODO: log4net config? Serilog?
			//const string loggerTemplate = @"{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u4}]<{ThreadId}> [{SourceContext:l}] {Message:lj}{NewLine}{Exception}";
			//var baseDir = AppDomain.CurrentDomain.BaseDirectory;
			//var logfile = Path.Combine(baseDir, "App_Data", "logs", "log.txt");
			//Log.Logger = new LoggerConfiguration()
			//	.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
			//	.Enrich.With(new ThreadIdEnricher())
			//	.Enrich.FromLogContext()
			//	.WriteTo.Console(LogEventLevel.Information, loggerTemplate, theme: AnsiConsoleTheme.Literate)
			//	.WriteTo.File(logfile, LogEventLevel.Information, loggerTemplate,
			//		rollingInterval: RollingInterval.Day, retainedFileCountLimit: 90)
			//	.CreateLogger();

			ILoggerFactory loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });

			Log.SetLoggerFactory(loggerFactory);

			using IHost host = CreateHostBuilder(args).Build();

			// Application code should start here.
			await host.RunAsync();

			Console.WriteLine("Press any key to finish");

			Console.ReadKey();
			await host.StopAsync();
		}

		static IHostBuilder CreateHostBuilder(string[] args)
		{
			return Host.CreateDefaultBuilder(args)
				.UseWindowsService()
				.ConfigureAppConfiguration(
					(hostingContext, configuration) =>
					{
						configuration.Sources.Clear();

						IHostEnvironment env = hostingContext.HostingEnvironment;

						// TODO: Go back to XML to allow comments in config files
						configuration
							.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
							.AddJsonFile($"appsettings.{env.EnvironmentName}.json", true, true);
					})
				.ConfigureServices((hostContext, services) =>
				{
					// Allow running as windows service by implementing HostedService
					services.AddHostedService<GuardianService>();

					// .net core dependency injection will provide configuration to constructor
					// of GuardianService:
					services.Configure<IConfiguration>(hostContext.Configuration);
				});
		}
	}
}
