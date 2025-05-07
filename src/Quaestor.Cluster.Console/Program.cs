using System;
using System.Collections.Generic;
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
		private const string _log4NetConfigFileName = "log4net.config";

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
					(_, configuration) =>
					{
						const string configFileName = "quaestor.cluster.config.yml";

						ConfigureApplication(configuration, configFileName);
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

		private static void ConfigureApplication(IConfigurationBuilder configuration,
		                                         string configFileName)
		{
			configuration.Sources.Clear();

			string defaultConfig =
				ConfigUtils.GetConfigFilePath(configFileName, _configDir,
					out List<string> searchedDirs);

			if (defaultConfig != null)
			{
				_logger.LogInformation("Configuration path: {configPath}", defaultConfig);
			}
			else
			{
				ConfigUtils.LogMissingConfigFile(configFileName, searchedDirs);
			}

			configuration.AddYamlFile(defaultConfig, optional: false, reloadOnChange: true);
		}

		private static void ConfigureLogging()
		{
			var log4NetPath =
				ConfigUtils.GetConfigFilePath(_log4NetConfigFileName, _configDir,
					out List<string> searchedDirs);

			const string logFileSuffix = "cluster";
			ConfigUtils.ConfigureLogging(log4NetPath, logFileSuffix);

			_logger = Log.CreateLogger<Program>();

			if (log4NetPath == null)
				ConfigUtils.LogMissingConfigFile(_log4NetConfigFileName, searchedDirs);
		}
	}
}
