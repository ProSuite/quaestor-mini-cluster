using System;
using System.Collections.Generic;

namespace Quaestor.LoadBalancing
{
	public class LoadReportComparer : IComparer<QualifiedService>
	{
		private readonly bool _ignoreServerCpu;

		public LoadReportComparer(bool ignoreServerCpu = false)
		{
			_ignoreServerCpu = ignoreServerCpu;
		}

		public int Compare(QualifiedService x, QualifiedService y)
		{
			if (x == null && y == null)
			{
				return 0;
			}

			if (x == null || y == null)
			{
				return x == null ? -1 : 1;
			}

			// If specified, use known load rate:
			if (x.KnownLoadRate >= 0 && y.KnownLoadRate >= 0)
			{
				double diff = x.KnownLoadRate - y.KnownLoadRate;

				if (diff != 0)
					return diff < 0 ? -1 : 1;
			}

			// Otherwise, use server stats
			if (x.ServerStats == null && y.ServerStats == null)
			{
				return 0;
			}

			if (x.ServerStats == null || y.ServerStats == null)
			{
				return x.ServerStats == null ? -1 : 1;
			}

			if (!_ignoreServerCpu)
			{
				// First compare host machine CPU utilization:
				double cpuUsageX = x.ServerStats.ServerUtilization;
				double cpuUsageY = y.ServerStats.ServerUtilization;

				double cpuDiff = cpuUsageX - cpuUsageY;

				if (cpuDiff != 0)
					return cpuDiff < 0 ? -1 : 1;
			}

			// Then, prioritize the service with the least requests per capacity
			double processUtilizationX =
				(double) x.ServerStats.CurrentRequests / x.ServerStats.RequestCapacity;
			double processUtilizationY =
				(double) y.ServerStats.CurrentRequests / y.ServerStats.RequestCapacity;

			if (processUtilizationX < processUtilizationY)
				return -1;

			if (processUtilizationX > processUtilizationY)
				return 1;

			// Even if they are exactly the same (e.g. several services with 0 requests on the same machine),
			// Make sure they have a deterministic order to allow for easy round-robin or other optimizations:
			int hostComparison = string.Compare(x.ServiceLocation.HostName,
				y.ServiceLocation.HostName, StringComparison.InvariantCultureIgnoreCase);

			if (hostComparison != 0)
				return hostComparison;

			int portDiff = x.ServiceLocation.Port - y.ServiceLocation.Port;

			return portDiff;
		}
	}
}
