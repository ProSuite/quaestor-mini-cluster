using System;

namespace Quaestor.LoadReporting
{
	public interface IServiceLoad
	{
		int ProcessCapacity { get; set; }
		int CurrentProcessCount { get; set; }
		double ServerUtilization { get; set; }

		DateTime ReportStart { get; }

		double KnownLoadRate { get; set; }

		void Reset();
	}
}
