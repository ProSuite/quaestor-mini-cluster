using System;
using System.Threading.Tasks;

namespace Quaestor.MiniCluster
{
	public interface IManagedProcess
	{
		string ProcessName { get; }

		bool MonitoringSuspended { get; set; }

		bool IsRunning { get; }

		int StartupTrialCount { get; }

		Task<bool> IsServingAsync();

		Task<bool> StartAsync();

		Task<bool> TryShutdownAsync(TimeSpan timeOut);

		void Kill();
	}
}
