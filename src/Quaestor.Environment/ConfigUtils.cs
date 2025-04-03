using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Quaestor.Environment
{
	public static class ConfigUtils
	{
		private static ILogger _logger;

		public static void ConfigureLogging(string log4NetConfigFilePath,
		                                    string logFileSuffix)
		{
			const string logSuffixEnvVar = "QUAESTOR_LOGFILE_SUFFIX";

			string logSuffixValue = System.Environment.GetEnvironmentVariable(logSuffixEnvVar);

			if (string.IsNullOrEmpty(logSuffixValue))
			{
				System.Environment.SetEnvironmentVariable(logSuffixEnvVar, logFileSuffix);
			}

			ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
			{
				builder.SetMinimumLevel(LogLevel.Debug);
				if (log4NetConfigFilePath != null)
				{
					builder.AddLog4Net(log4NetConfigFilePath);
				}

				builder.AddConsole();

				// Surely there must be a better way... or use log4net console configuration
				builder.AddFilter(
					(provider, _, logLevel) =>
						!provider.Contains("ConsoleLoggerProvider")
						|| logLevel >= LogLevel.Information);
			});

			Log.SetLoggerFactory(loggerFactory);

			_logger = Log.CreateLogger(nameof(ConfigUtils));

			string logConfiguration = log4NetConfigFilePath ?? "Console logging";

			_logger.LogInformation("Logging configured based on {logConfig}", logConfiguration);
		}

		public static void LogApplicationStart(string[] args)
		{
			string processName = Process.GetCurrentProcess().ProcessName;

			Assembly executingAssembly = Assembly.GetExecutingAssembly();
			string path = Path.GetDirectoryName(executingAssembly.Location);

			string bits = System.Environment.Is64BitProcess ? "64 bit" : "32 bit";

			_logger.LogDebug(
				"{file} ({bits}) version {version} started from {path} with the following arguments: {args}",
				processName, bits, executingAssembly.GetName().Version, path,
				string.Join(" ", args));

			string frameworkDescription = RuntimeInformation.FrameworkDescription;

			_logger.LogDebug("Currently used .NET Runtime: {netRuntime}", frameworkDescription);
		}

		public static string GetLog4NetConfigPath([NotNull] string log4NetConfigFileName,
		                                          [CanBeNull] string configDir)
		{
			string log4NetPath = null;

			if (!string.IsNullOrEmpty(configDir))
			{
				string path = Path.Combine(configDir, log4NetConfigFileName);

				if (File.Exists(path))
				{
					return path;
				}
			}

			if (File.Exists(log4NetConfigFileName))
			{
				log4NetPath = log4NetConfigFileName;
			}
			else if (File.Exists(Path.Combine(@"..", log4NetConfigFileName)))
			{
				log4NetPath = Path.Combine(@"..", log4NetConfigFileName);
			}

			return log4NetPath;
		}
	}
}
