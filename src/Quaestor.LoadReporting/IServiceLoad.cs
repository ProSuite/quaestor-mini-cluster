using System;
using JetBrains.Annotations;

namespace Quaestor.LoadReporting
{
	[PublicAPI]
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
