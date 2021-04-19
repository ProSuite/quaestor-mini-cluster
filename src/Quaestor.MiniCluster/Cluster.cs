using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;
using Quaestor.KeyValueStore;
using Quaestor.Utilities;

namespace Quaestor.MiniCluster
{
	public class Cluster
	{
		private readonly ClusterConfig _clusterConfig;
		private readonly ILogger _logger;

		private readonly List<IManagedProcess> _managedProcesses = new List<IManagedProcess>();

		private readonly ClusterHeartBeat _heartbeat;

		public Cluster(string name = "<no name>")
		{
			Name = name;

			_logger = Log.CreateLogger($"Cluster {Name}");

			_clusterConfig = new ClusterConfig();

			_heartbeat = new ClusterHeartBeat(this, CareForUnhealthy, CareForUnavailable);
		}

		public Cluster(ClusterConfig clusterConfig) : this(clusterConfig.Name)
		{
			_clusterConfig = clusterConfig;
		}

		public ServiceRegistrar ServiceRegistrar { get; set; }

		public string Name { get; }

		public IReadOnlyCollection<IManagedProcess> Members => _managedProcesses.AsReadOnly();

		public TimeSpan HeartBeatInterval =>
			TimeSpan.FromSeconds(_clusterConfig.HeartBeatIntervalSeconds);

		public TimeSpan MemberResponseTimeOut =>
			TimeSpan.FromSeconds(_clusterConfig.MemberResponseTimeOutSeconds);

		public TimeSpan MemberMaxShutdownTime =>
			TimeSpan.FromSeconds(_clusterConfig.MemberMaxShutdownTimeSeconds);

		public int MemberMaxStartupRetries => _clusterConfig.MemberMaxStartupRetries;

		public void Add(IManagedProcess managedProcess)
		{
			_managedProcesses.Add(managedProcess);
		}

		public void AddRange(IEnumerable<IManagedProcess> managedProcesses)
		{
			_managedProcesses.AddRange(managedProcesses);
		}

		public async Task<bool> StartAsync()
		{
			_logger.LogInformation("Starting cluster...");

			CheckRunningProcesses();

			// Ensure KVS is running, if configured!
			IKeyValueStore keyValueStore = await InitializeKeyValueStoreAsync(Members, _heartbeat);

			ServiceRegistrar = new ServiceRegistrar(new ServiceRegistry(keyValueStore));

			await _heartbeat.StartAsync();

			return true;
		}

		private static async Task<IKeyValueStore> InitializeKeyValueStoreAsync(
			[NotNull] IEnumerable<IManagedProcess> managedProcesses,
			[NotNull] ClusterHeartBeat heartbeat)
		{
			string etcdAgentType = WellKnownAgentType.KeyValueStore.ToString();

			IEnumerable<IManagedProcess> kvsProcesses =
				managedProcesses.Where(m => m.AgentType.Equals(etcdAgentType));

			// For all well-known KVS agent processes...
			foreach (var kvsProcess in kvsProcesses)
			{
				// Check if they live (reviving them, if necessary):
				bool started = await heartbeat.CheckHeartBeatAsync(kvsProcess);

				if (started)
				{
					// ... and try making contact
					IServerProcess serverProcess = (IServerProcess) kvsProcess;

					var keyValueStore = await EtcdKeyValueStore.TryConnectAsync(
						serverProcess.HostName, serverProcess.Port, serverProcess.UseTls);

					if (keyValueStore != null)
					{
						return keyValueStore;
					}
				}
			}

			// None is configured or none is running or none is responding:
			return new LocalKeyValueStore();
		}

		public async Task<bool> ShutdownAsync(TimeSpan timeout)
		{
			await _heartbeat.ShutdownAsync();

			var shutDownResults = await Task.WhenAll(Members.Select(m =>
			{
				if (m is IServerProcess serverProcess)
				{
					ServiceRegistrar?.EnsureRemoved(serverProcess);
				}

				if (m.ClusterShutdownAction == ShutdownAction.Kill)
				{
					m.Kill();
				}

				//return m.TryShutdownAsync(timeout);

				return Task.FromResult(true);
			}));

			return shutDownResults.All(r => r);
		}

		public void Abort()
		{
			_heartbeat.ShutdownAsync();

			foreach (var process in Members)
			{
				if (process is IServerProcess serverProcess)
				{
					ServiceRegistrar?.EnsureRemoved(serverProcess);
				}

				process.Kill();
			}
		}

		private async Task<bool> CareForUnhealthy(IManagedProcess process)
		{
			// TODO: Consider adding a new process while still shutting down, but only if ephemeral ports are used

			_logger.LogInformation("(Re-)starting process with status 'not serving': {process}",
				process);

			if (process.IsKnownRunning)
			{
				process.MonitoringSuspended = true;
				try
				{
					if (process is IServerProcess serverProcess)
					{
						ServiceRegistrar?.EnsureRemoved(serverProcess);
					}

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
			{
				_logger.LogWarning("Startup retries have been exceeded. Not starting {process}",
					process);

				return false;
			}

			return await TryStart(process);
		}

		private async Task<bool> TryStart([NotNull] IManagedProcess process)
		{
			bool success = await process.StartAsync();

			if (!success)
			{
				process.StartupFailureCount++;
			}
			else if (process is IServerProcess serverProcess)
			{
				ServiceRegistrar?.Ensure(serverProcess);
			}

			return success;
		}

		private async Task<bool> CareForUnavailable([NotNull] IManagedProcess process)
		{
			_logger.LogInformation("(Re-)starting process due to request time-out: {process}",
				process);

			if (process is IServerProcess serverProcess)
			{
				ServiceRegistrar?.EnsureRemoved(serverProcess);
			}

			if (process.StartupFailureCount > MemberMaxStartupRetries)
			{
				_logger.LogWarning("Startup retries have been exceeded. Not starting {process}",
					process);
			}
			else
			{
				return await TryStart(process);
			}

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

		public void ClearMembers()
		{
			// TODO: Graceful Shutdown

			_managedProcesses.Clear();
		}
	}
}
