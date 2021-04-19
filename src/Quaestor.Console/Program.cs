using System;
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
		const string _log4NetConfigFileName = "log4net.config";

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

			ConfigureLogging();

			LogApplicationStart(args);

			using IHost host = CreateHostBuilder(args).Build();

			try
			{
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
			return SetOptions((QuaestorOptions) opts);
		}

		private static bool SetOptions(QuaestorClusterOptions opts)
		{
			return SetOptions((QuaestorOptions) opts);
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
					(hostingContext, configuration) =>
					{
						configuration.Sources.Clear();

						IHostEnvironment env = hostingContext.HostingEnvironment;

						_logger.LogInformation(
							"Configuring application for hosting environment {env}",
							env.EnvironmentName);

						_logger.LogInformation("Configuration path: {rootPath}",
							env.ContentRootPath);

						string defaultConfig = Path.Combine(_configDir, "quaestor.config.yml");
						string envSpecificConfig = Path.Combine(_configDir,
							$"quaestor.config.{env.EnvironmentName}.yml");

						configuration
							.AddYamlFile(defaultConfig, optional: true, reloadOnChange: true)
							.AddYamlFile(envSpecificConfig, true, true);
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

		private static void ConfigureLogging()
		{
			var log4NetPath = GetLog4NetConfigPath();

			const string logSuffixEnvVar = "QUAESTOR_LOGFILE_SUFFIX";

			string logSuffixValue = System.Environment.GetEnvironmentVariable(logSuffixEnvVar);

			if (string.IsNullOrEmpty(logSuffixValue))
			{
				string mode = _loadBalancerMode ? "load_balancer" : "cluster";
				System.Environment.SetEnvironmentVariable(logSuffixEnvVar, mode);
			}

			ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
			{
				builder.SetMinimumLevel(LogLevel.Debug);
				if (log4NetPath != null)
				{
					builder.AddLog4Net(log4NetPath);
				}

				builder.AddConsole();

				// Surely there must be a better way... or use log4net console configuration
				builder.AddFilter(
					(provider, _, logLevel) =>
						!provider.Contains("ConsoleLoggerProvider")
						|| logLevel >= LogLevel.Information);
			});

			Log.SetLoggerFactory(loggerFactory);

			_logger = Log.CreateLogger<Program>();

			string logConfiguration = log4NetPath ?? "Console logging";

			_logger.LogInformation("Logging configured based on {logConfig}", logConfiguration);
		}

		private static string GetLog4NetConfigPath()
		{
			string log4NetPath = null;

			if (!string.IsNullOrEmpty(_configDir))
			{
				string path = Path.Combine(_configDir, _log4NetConfigFileName);

				if (File.Exists(path))
				{
					return path;
				}
			}

			if (File.Exists(_log4NetConfigFileName))
			{
				log4NetPath = _log4NetConfigFileName;
			}
			else if (File.Exists(Path.Combine(@"..", _log4NetConfigFileName)))
			{
				log4NetPath = Path.Combine(@"..", _log4NetConfigFileName);
			}

			return log4NetPath;
		}
	}
}
