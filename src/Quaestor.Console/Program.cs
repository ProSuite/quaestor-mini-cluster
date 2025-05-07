using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CommandLine;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;
using Quaestor.LoadBalancing;
using Quaestor.MiniCluster;

namespace Quaestor.Console
{
	[UsedImplicitly]
	internal class Program
	{
		private const string _log4NetConfigFileName = "log4net.config";

		private static ILogger<Program> _logger;

		private static bool _loadBalancerMode;

		[NotNull] private static string _configDir = string.Empty;

		private static async Task<int> Main(string[] args)
		{
			bool argumentsOk = Parser.Default
				.ParseArguments<QuaestorClusterOptions, QuaestorLoadBalancerOptions>(args)
				.MapResult(
					(QuaestorClusterOptions opts) => SetOptions(opts),
					(QuaestorLoadBalancerOptions opts) => SetOptions(opts),
					_ => false);

			if (!argumentsOk)
			{
				return 1;
			}

			try
			{
				ConfigureLogging();

				LogApplicationStart(args);

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

		private static bool SetOptions(QuaestorLoadBalancerOptions opts)
		{
			_loadBalancerMode = true;
			return SetOptions((QuaestorOptions)opts);
		}

		private static bool SetOptions(QuaestorClusterOptions opts)
		{
			return SetOptions((QuaestorOptions)opts);
		}

		private static bool SetOptions(QuaestorOptions opts)
		{
			_configDir = opts.ConfigDirectory ?? string.Empty;
			return true;
		}

		private static void LogApplicationStart(string[] args)
		{
			Assembly executingAssembly = Assembly.GetExecutingAssembly();

			string file = Path.GetFileNameWithoutExtension(executingAssembly.Location);
			string path = Path.GetDirectoryName(executingAssembly.Location);

			string bits = System.Environment.Is64BitProcess ? "64 bit" : "32 bit";

			_logger.LogDebug(
				"{file} ({bits}) version {version} started from {path} with the following arguments: {args}",
				file, bits, executingAssembly.GetName().Version, path, string.Join(' ', args));
		}

		private static IHostBuilder CreateHostBuilder(string[] args)
		{
			return Host.CreateDefaultBuilder(args)
				.UseWindowsService()
				.ConfigureAppConfiguration(
					(_, configuration) =>
					{
						const string configFileName = "quaestor.config.yml";

						ConfigureApplication(configuration, configFileName);
					})
				.ConfigureServices((hostContext, services) =>
				{
					if (args.Length == 0 || args[0].Equals("cluster"))
					{
						// Allow running as windows service by implementing HostedService
						services.AddHostedService<AgentGuardianService>();
					}
					else if (args.Length > 0 && args[0].StartsWith("load-balance"))
					{
						services.AddHostedService<LoadBalancingService>();
					}
					else
					{
						_logger.LogError("Invalid arguments. Specify cluster or load-balance");
						return;
					}

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

			string logFileSuffix = _loadBalancerMode ? "load_balancer" : "cluster";
			ConfigUtils.ConfigureLogging(log4NetPath, logFileSuffix);

			_logger = Log.CreateLogger<Program>();

			if (log4NetPath == null)
				ConfigUtils.LogMissingConfigFile(_log4NetConfigFileName, searchedDirs);
		}
	}
}
