using System;

namespace Quaestor.LoadReporting
{
	public interface IServiceLoad
	{
		/// <summary>
		///     The capacity of the service.
		/// </summary>
		int ProcessCapacity { get; set; }

		/// <summary>
		///     The number of currently ongoing processes.
		/// </summary>
		int CurrentProcessCount { get; }

		/// <summary>
		///     The server utilization (CPU usage) ratio
		/// </summary>
		double ServerUtilization { get; }

		/// <summary>
		///     The host machine's memory usage percentage.
		/// </summary>
		double ServerMemoryUsagePercent { get; }

		/// <summary>
		///     The time of the last memory usage measurement.
		/// </summary>
		DateTime? LastUpdateTime { get; }

		double KnownLoadRate { get; set; }

		void ResetReportStart();
	}
}
