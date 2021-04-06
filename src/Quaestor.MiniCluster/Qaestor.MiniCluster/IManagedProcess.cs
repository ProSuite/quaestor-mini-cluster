using System;
using System.Threading.Tasks;

namespace Quaestor.MiniCluster
{
	public interface IManagedProcess
	{
		/// <summary>
		///     The name of the process.
		/// </summary>
		string ProcessName { get; }

		/// <summary>
		///     If true, this instance will not be monitored for heart beats and its health status
		///     will not be checked.
		/// </summary>
		bool MonitoringSuspended { get; set; }

		/// <summary>
		///     Whether the process is currently running or not.
		/// </summary>
		bool IsRunning { get; }

		/// <summary>
		///     The number of times the re-start process has failed.
		/// </summary>
		int StartupFailureCount { get; set; }

		/// <summary>
		///     Whether the process is serving as per GRPC health check protocol.
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
	}
}
