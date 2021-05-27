using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Health.V1;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;
using Quaestor.Utilities;

namespace Quaestor.MiniCluster
{
	/// <summary>
	///     Process that can be started directly on the local machine and that can be
	///     reached with the specified connection details in order to check its health.
	/// </summary>
	public class LocalProcess : IManagedProcess, IServerProcess
	{
		private const string _localhost = "127.0.0.1";

		private readonly ILogger _logger = Log.CreateLogger<LocalProcess>();

		private readonly ChannelCredentials _credentials;

		private Health.HealthClient _healthClient;

		/// <summary>
		///     Initializes a new instance of the <see cref="LocalProcess" /> class.
		/// </summary>
		/// <param name="agentType">
		///     The type of agent. Possibly one of the
		///     <see cref="WellKnownAgentType" /> types.
		/// </param>
		/// <param name="executablePath">The path to the executable.</param>
		/// <param name="commandLineArguments"></param>
		/// <param name="hostName">The process' service host name</param>
		/// <param name="port">The process' service port number</param>
		/// <param name="credentials">
		///     Client credentials to connect to the
		///     process' service.
		/// </param>
		public LocalProcess([NotNull] string agentType,
		                    [NotNull] string executablePath,
		                    [CanBeNull] string commandLineArguments,
		                    [CanBeNull] string hostName = _localhost,
		                    int port = -1,
		                    [CanBeNull] ChannelCredentials credentials = null)
		{
			AgentType = agentType;
			ExecutablePath = executablePath;
			CommandLineArguments = commandLineArguments;

			HostName = hostName;
			Port = port;

			_credentials = credentials ?? ChannelCredentials.Insecure;
		}

		public string ExecutablePath { get; }

		[CanBeNull] public string CommandLineArguments { get; }
		public Process Process { get; set; }

		public Channel Channel { get; private set; }

		/// <summary>
		///     If true, an unhealthy process is killed right away to expedite the restart process.
		///     If false, ongoing requests are finished before shutting down the process.
		/// </summary>
		public bool PrioritizeAvailability { get; set; }

		public Dictionary<string, string> EnvironmentVariables { get; set; }

		#region IServerProcess members

		public string HostName { get; }

		public int Port { get; private set; }

		public bool UseTls => _credentials != ChannelCredentials.Insecure;

		public IList<string> ServiceNames { get; } = new List<string>();

		#endregion

		#region IManagedProcess members

		public string AgentType { get; }

		public string ProcessName => Path.GetFileNameWithoutExtension(ExecutablePath);

		/// <summary>
		///     If true, this instance will not be monitored for heart beats and its health status
		///     will not be checked.
		/// </summary>
		public bool MonitoringSuspended { get; set; }

		public int StartupFailureCount { get; set; }

		public bool IsKnownRunning => !Process?.HasExited ?? false;

		public ShutdownAction ClusterShutdownAction { get; set; }

		public async Task<bool> IsServingAsync()
		{
			if (Port < 0)
			{
				// Avoid waiting for the timeout of the health check, if possible.
				return false;
			}

			if (_healthClient == null)
			{
				OpenChannel();
			}

			return await AreServicesHealthyAsync();
		}

		public async Task<bool> StartAsync()
		{
			MonitoringSuspended = true;

			EnsureDead(Process);

			string commandLineArgs = string.Empty;

			string executablePath = ExecutablePath;

			try
			{
				if (Port < 0)
				{
					Port = GrpcUtils.GetFreeTcpPort();
				}

				commandLineArgs = CommandLineArguments?
					.Replace("{HostName}", HostName)
					.Replace("{Port}", Port.ToString());

				executablePath = GetActualExePath();

				_logger.LogInformation(
					"Starting {executablePath} with parameters {commandLineArgs}...",
					executablePath, commandLineArgs);

				Process = ProcessUtils.StartProcess(executablePath, commandLineArgs,
					false, true, EnvironmentVariables);

				// Very important. Otherwise the process hangs once the buffer is full.
				Process.BeginOutputReadLine();
				Process.BeginErrorReadLine();

				// Configurable? Or don't even wait for the health check and only start it?
				TimeSpan startupTimeAverage = TimeSpan.FromSeconds(8);

				// Let the process start the services...
				await Task.Delay(startupTimeAverage);

				bool healthy = await IsServingAsync();

				if (healthy)
				{
					_logger.LogInformation("Successfully started {process}", this);
				}

				return healthy;
			}
			catch (Exception e)
			{
				string errorMessage = e.Message;

				_logger.LogError(e,
					"Error starting {ExecutablePath} with parameters {CommandLineArguments}: {errorMessage}",
					executablePath, commandLineArgs, errorMessage);

				return false;
			}
			finally
			{
				MonitoringSuspended = false;
			}
		}

		public async Task<bool> TryShutdownAsync(TimeSpan timeOut)
		{
			// TODO: Shutdown API (ProcessManagementService) or Shutdown actor

			if (Process == null || Process.HasExited)
			{
				return true;
			}

			if (PrioritizeAvailability)
			{
				_logger.LogDebug("Process {process} is killed to prioritize availability...)",
					this);

				Kill();
			}
			else
			{
				_logger.LogDebug("Waiting for shutdown ({timeOut}s...)", timeOut.TotalSeconds);

				// TODO: Send shut down signal

				await Task.Delay(timeOut);
			}

			return Process.HasExited;
		}

		public void Kill()
		{
			EnsureDead(Process);
		}

		#endregion

		public override string ToString()
		{
			int? pid = Process?.Id;

			string processIdentifier = pid?.ToString() ?? "<not started>";

			return
				$"Agent type {AgentType}, process name {ProcessName}, PID: {processIdentifier}, Host: {HostName}, Port: {Port}";
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj))
				return false;
			if (ReferenceEquals(this, obj))
				return true;
			if (obj.GetType() != GetType())
				return false;
			return Equals((LocalProcess) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = (AgentType != null
					? StringComparer.InvariantCultureIgnoreCase.GetHashCode(AgentType)
					: 0);
				hashCode = (hashCode * 397) ^ (ExecutablePath != null
					? StringComparer.InvariantCultureIgnoreCase.GetHashCode(ExecutablePath)
					: 0);
				hashCode = (hashCode * 397) ^ (CommandLineArguments != null
					? StringComparer.InvariantCultureIgnoreCase.GetHashCode(CommandLineArguments)
					: 0);
				hashCode = (hashCode * 397) ^ (HostName != null
					? StringComparer.InvariantCultureIgnoreCase.GetHashCode(HostName)
					: 0);
				hashCode = (hashCode * 397) ^ Port;
				hashCode = (hashCode * 397) ^
				           (ServiceNames != null ? ServiceNames.GetHashCode() : 0);
				return hashCode;
			}
		}

		private bool Equals(LocalProcess other)
		{
			return string.Equals(AgentType, other.AgentType,
				       StringComparison.InvariantCultureIgnoreCase) &&
			       string.Equals(ExecutablePath, other.ExecutablePath,
				       StringComparison.InvariantCultureIgnoreCase) &&
			       string.Equals(CommandLineArguments, other.CommandLineArguments,
				       StringComparison.InvariantCultureIgnoreCase) &&
			       string.Equals(HostName, other.HostName,
				       StringComparison.InvariantCultureIgnoreCase) &&
			       Port == other.Port && Equals(ServiceNames, other.ServiceNames);
		}

		private void OpenChannel()
		{
			if (string.IsNullOrEmpty(HostName))
				throw new ArgumentNullException(nameof(HostName));

			Channel = GrpcUtils.CreateChannel(HostName, Port, _credentials);

			_logger.LogDebug("Created grpc channel to {HostName} on port {Port}", HostName, Port);

			_healthClient = new Health.HealthClient(Channel);
		}

		private async Task<bool> AreServicesHealthyAsync()
		{
			if (ServiceNames.Count == 0)
			{
				_logger.LogInformation(
					"{agentType}: No service names to check, using empty string.", AgentType);

				return await CheckHealthAsync(string.Empty);
			}

			foreach (string serviceName in ServiceNames)
			{
				bool healthy = await CheckHealthAsync(serviceName);

				if (!healthy)
				{
					return false;
				}
			}

			return true;
		}

		private async Task<bool> CheckHealthAsync(string serviceName)
		{
			var statusCode = await GrpcUtils.IsServingAsync(_healthClient, serviceName);

			return statusCode == StatusCode.OK;
		}

		private string GetActualExePath()
		{
			string executablePath = ExecutablePath;

			if (!File.Exists(executablePath))
			{
				_logger.LogDebug("{executablePath} was not found!", executablePath);

				if (AssumeIsRelativePath(executablePath))
				{
					// It's a relative path that does not exist from the current dir:

					Assembly exeAssembly = Assembly.GetEntryAssembly();

					string exePath = exeAssembly?.Location;

					if (exePath == null)
					{
						return executablePath;
					}

					string exeDir = Path.GetDirectoryName(exePath);

					executablePath = Path.Combine(exeDir, executablePath);

					_logger.LogDebug("Using relative path {executablePath} from executable...",
						executablePath);
				}
			}

			return executablePath;
		}

		private static bool AssumeIsRelativePath(string executablePath)
		{
			if (Path.IsPathRooted(executablePath))
			{
				return false;
			}

			return executablePath.StartsWith(".") ||
			       executablePath.StartsWith(Path.DirectorySeparatorChar.ToString());
		}

		private void EnsureDead(Process process)
		{
			try
			{
				if (process != null && !process.HasExited)
				{
					_logger.LogDebug("Killing {process}...)", this);

					process.Kill();
				}
			}
			catch (Exception e)
			{
				_logger.LogWarning(e, "Error killing the started process {process}", process);
			}
		}
	}
}
