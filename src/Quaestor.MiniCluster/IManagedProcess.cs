using System;
using System.Threading.Tasks;
using Quaestor.Environment;

namespace Quaestor.MiniCluster
{
	public interface IManagedProcess
	{
		/// <summary>
		///     The name of the process.
		/// </summary>
		string ProcessName { get; }

		/// <summary>
		///     The type of agent. Possibly one of the the WellKnownAgentType types./>
		/// </summary>
		string AgentType { get; }

		/// <summary>
		///     If true, this instance will not be monitored for heart beats and its health status
		///     will not be checked.
		/// </summary>
		bool MonitoringSuspended { get; set; }

		/// <summary>
		///     Whether the process is currently running or not.
		/// </summary>
		bool IsKnownRunning { get; }

		/// <summary>
		///     The action to be performed for this agent when the cluster shuts down.
		/// </summary>
		ShutdownAction ClusterShutdownAction { get; set; }

		/// <summary>
		///     The number of times the re-start process has failed.
		/// </summary>
		int StartupFailureCount { get; set; }

		/// <summary>
		///     Whether the process is due for recycling and the cluster manager is allowed a shut-down
		///     and re-start of this process.
		/// </summary>
		bool IsDueForRecycling { get; }

		/// <summary>
		///     Whether the process is serving as per GRPC health check protocol. If false is returned,
		///     the process can be re-started by the cluster.
		/// </summary>
		/// <returns></returns>
		Task<bool> IsServingAsync();

		/// <summary>
		///     Starts the process.
		/// </summary>
		/// <returns></returns>
		Task<bool> StartAsync();

		/// <summary>
		///     Attempts a graceful shutdown of the process while finishing ongoing requests.
		/// </summary>
		/// <param name="timeOut"></param>
		/// <returns></returns>
		Task<bool> TryShutdownAsync(TimeSpan timeOut);

		/// <summary>
		///     Kills the process immediately.
		/// </summary>
		void Kill();

		/// <summary>
		///     Gets the number of currently ongoing requests inside this process.
		/// </summary>
		/// <returns></returns>
		Task<int?> GetOngoingRequestCountAsync();
	}
}
