using System;
using JetBrains.Annotations;

namespace Quaestor.LoadReporting
{
	[PublicAPI]
	public interface IServiceLoad
	{
		int ProcessCapacity { get; set; }
		int CurrentProcessCount { get; set; }
		double CpuUsage { get; set; }

		int ClientCallsFinished { get; set; }
		int ClientCallsStarted { get; set; }
		DateTime ReportStart { get; }

		void ResetClientStats();
	}
}
