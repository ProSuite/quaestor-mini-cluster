using System;
using System.IO;
using System.Threading.Tasks;
using CommandLine;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;
using Quaestor.MiniCluster;

namespace Quaestor.Cluster.Console
{
	[UsedImplicitly]
	internal class Program
	{
		const string _log4NetConfigFileName = "log4net.config";

		private static ILogger<Program> _logger;

		[NotNull] private static string _configDir = string.Empty;

		private static async Task<int> Main(string[] args)
		{
			var parsedArgs = Parser.Default
				.ParseArguments<QuaestorClusterOptions>(args);

			parsedArgs.WithParsed(opts => SetOptions(opts));

			try
			{
				ConfigureLogging();

				ConfigUtils.LogApplicationStart(args);

				using IHost host = CreateHostBuilder(args).Build();

				await host.RunAsync();
			}
			catch (Exception e)
			{
				_logger.LogError(e, "Error: {message}", e.Message);
				return -1;
			}

			return 0;
		}

		private static bool SetOptions(QuaestorClusterOptions opts)
		{
			_configDir = opts.ConfigDirectory ?? string.Empty;
			return true;
		}

		private static IHostBuilder CreateHostBuilder(string[] args)
		{
			return Host.CreateDefaultBuilder(args)
				.UseWindowsService()
				.ConfigureAppConfiguration(
					(hostingContext, configuration) =>
					{
						configuration.Sources.Clear();

						IHostEnvironment env = hostingContext.HostingEnvironment;

						_logger.LogInformation(
							"Configuring application for hosting environment {env}",
							env.EnvironmentName);

						_logger.LogInformation("Configuration path: {rootPath}",
							string.IsNullOrEmpty(_configDir) ? env.ContentRootPath : _configDir);

						string defaultConfig =
							Path.Combine(_configDir, "quaestor.cluster.config.yml");
						string envSpecificConfig = Path.Combine(_configDir,
							$"quaestor.cluster.config.{env.EnvironmentName}.yml");

						if (File.Exists(defaultConfig))
						{
							_logger.LogInformation("Using configuration file: {configFile}",
								defaultConfig);
						}

						if (File.Exists(defaultConfig))
						{
							_logger.LogInformation("Using configuration file: {configFile}",
								envSpecificConfig);
						}

						configuration
							.AddYamlFile(defaultConfig, optional: true, reloadOnChange: true)
							.AddYamlFile(envSpecificConfig, true, true);
					})
				.ConfigureServices((hostContext, services) =>
				{
					// Allow running as windows service by implementing HostedService
					services.AddHostedService<AgentGuardianService>();

					// .net core dependency injection will provide configuration to constructor
					// of GuardianService:
					services.Configure<IConfiguration>(hostContext.Configuration);
				});
		}

		private static void ConfigureLogging()
		{
			var log4NetPath = ConfigUtils.GetLog4NetConfigPath(_log4NetConfigFileName, _configDir);

			const string logFileSuffix = "cluster";
			ConfigUtils.ConfigureLogging(log4NetPath, logFileSuffix);

			_logger = Log.CreateLogger<Program>();
		}
	}
}
