using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;

namespace Quaestor.MiniCluster
{
	public class Cluster
	{
		private readonly ClusterConfig _clusterConfig;
		private readonly ILogger _logger;

		private readonly List<IManagedProcess> _managedProcesses = new List<IManagedProcess>();

		private ClusterHeartBeat _heartbeat;

		public Cluster(string name = "<no name>")
		{
			Name = name;

			_logger = Log.CreateLogger($"Cluster {Name}");

			_clusterConfig = new ClusterConfig();
		}

		public Cluster(ClusterConfig clusterConfig) : this(clusterConfig.Name)
		{
			_clusterConfig = clusterConfig;
		}

		public string Name { get; }

		public IReadOnlyCollection<IManagedProcess> Members => _managedProcesses.AsReadOnly();

		public TimeSpan HeartBeatInterval => TimeSpan.FromSeconds(_clusterConfig.HeartBeatIntervalSeconds);

		public TimeSpan MemberResponseTimeOut => TimeSpan.FromSeconds(_clusterConfig.MemberResponseTimeOutSeconds);

		public TimeSpan MemberMaxShutdownTime => TimeSpan.FromSeconds(_clusterConfig.MemberMaxShutdownTimeSeconds);

		public int MemberMaxStartupRetries => _clusterConfig.MemberMaxStartupRetries;

		public void Add(IManagedProcess managedProcess)
		{
			_managedProcesses.Add(managedProcess);
		}

		public void AddRange(IEnumerable<IManagedProcess> managedProcesses)
		{
			_managedProcesses.AddRange(managedProcesses);
		}

		public void Start()
		{
			_logger.LogInformation("Starting cluster...");

			CheckRunningProcesses();

			_heartbeat = new ClusterHeartBeat(this, CareForUnhealthy, CareForUnavailable);
			_heartbeat.StartAsync();
		}

		public async Task<bool> ShutdownAsync(TimeSpan timeout)
		{
			await _heartbeat.ShutdownAsync();

			var shutDownResults = await Task.WhenAll(Members.Select(m => m.TryShutdownAsync(timeout)));

			return shutDownResults.All(r => r);
		}

		public void Abort()
		{
			_heartbeat.ShutdownAsync();

			foreach (var managedProcess in Members)
			{
				managedProcess.Kill();
			}
		}

		private async Task<bool> CareForUnhealthy(IManagedProcess process)
		{
			// TODO: Consider adding a new process while still shutting down, but only if ephemeral ports are used

			_logger.LogInformation("(Re-)starting process with status 'not serving': {process}", process);

			if (process.IsRunning)
			{
				process.MonitoringSuspended = true;
				try
				{
					var isShutDown = await process.TryShutdownAsync(MemberMaxShutdownTime);

					if (!isShutDown)
					{
						_logger.LogDebug(
							"Process has not shut down within {maxProcessTime}s. We have to kill it.",
							MemberMaxShutdownTime.TotalSeconds);

						process.Kill();
					}
				}
				finally
				{
					process.MonitoringSuspended = false;
				}
			}

			if (process.StartupFailureCount > MemberMaxStartupRetries)
				_logger.LogWarning("Startup retries have been exceeded. Not starting {process}", process);
			else
				return await TryStart(process);

			return true;
		}

		private static async Task<bool> TryStart([NotNull] IManagedProcess process)
		{
			bool success = await process.StartAsync();

			if (!success)
			{
				process.StartupFailureCount++;
			}

			return success;
		}

		private async Task<bool> CareForUnavailable([NotNull] IManagedProcess process)
		{
			_logger.LogInformation("(Re-)starting process due to request time-out: {process}", process);

			if (process.StartupFailureCount > MemberMaxStartupRetries)
				_logger.LogWarning("Startup retries have been exceeded. Not starting {process}", process);
			else
				return await TryStart(process);

			return true;
		}

		private void CheckRunningProcesses()
		{
			foreach (var processName in Members.Select(m => m.ProcessName).Distinct())
			{
				var processesCount = ProcessUtils.RunningProcessesCount(processName);

				if (processesCount > 0)
				{
					_logger.LogWarning(
						"{processesCount} potentially orphaned process(es) already running with the same name as {exePath}.",
						processesCount, processName);
				}
			}
		}
	}
}
