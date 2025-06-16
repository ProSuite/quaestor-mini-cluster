using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
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

		public static string GetConfigFilePath([NotNull] string configFileName,
		                                       [CanBeNull] string configuredSearchDir,
		                                       out List<string> searchedDirs)
		{
			string result = null;
			searchedDirs = new List<string>();

			if (TryFindFile(configuredSearchDir, out string fullPath, configFileName))
				return fullPath;

			searchedDirs.Add(configuredSearchDir);

			// TODO: Add C:\ProgramData\Dira GeoSystems\Quaestor

			string assemblyLocation = Assembly.GetExecutingAssembly().Location;
			string exeDir = Directory.GetParent(assemblyLocation)?.FullName;

			if (TryFindFile(exeDir, out fullPath, configFileName))
				return fullPath;

			searchedDirs.Add(exeDir);

			string oneUp = Directory.GetParent(exeDir)?.FullName;

			if (TryFindFile(oneUp, out fullPath, configFileName))
				return fullPath;

			searchedDirs.Add(oneUp);

			// For backward compatibility, check the current directory:
			if (File.Exists(configFileName))
			{
				result = configFileName;
			}

			searchedDirs.Add($"<current directory>");

			return result;
		}

		public static void LogMissingConfigFile(string configFileName,
		                                        List<string> searchedDirs)
		{
			_logger.LogWarning(
				"The configuration file {configFile} was not found in any of the searched directories:",
				configFileName);

			foreach (string searchedDir in searchedDirs)
			{
				_logger.LogInformation($"  - {searchedDir}");
			}
		}

		private static bool TryFindFile(string searchDir, out string result, string fileName)
		{
			result = null;

			if (string.IsNullOrEmpty(searchDir))
				return false;

			string path = Path.Combine(searchDir, fileName);

			if (File.Exists(path))
			{
				result = path;
				return true;
			}

			return false;
		}
	}
}
