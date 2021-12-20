using System;
using System.Diagnostics;

namespace Quaestor.LoadReporting
{
	public class ServiceLoad : IServiceLoad
	{
		DateTime? _lastGetCpuUsageTime;
		private TimeSpan _lastTotalProcessorTime;

		public ServiceLoad(int processCapacity = -1)
		{
			ProcessCapacity = processCapacity;

			Reset();

			// Initialize last CPU time:
			GetCpuUsage();
		}

		public DateTime ReportStart { get; private set; }

		public double KnownLoadRate { get; set; } = -1;

		public int ProcessCapacity { get; set; }
		public int CurrentProcessCount { get; set; }
		public double ServerUtilization { get; set; }

		// ReSharper disable once UnusedMember.Global (PublicAPI)
		public void StartRequest()
		{
			CurrentProcessCount++;
		}

		// ReSharper disable once UnusedMember.Global (PublicAPI)
		public void EndRequest()
		{
			CurrentProcessCount--;
		}

		public double GetCpuUsage()
		{
			Process p = Process.GetCurrentProcess();

			if (_lastGetCpuUsageTime == null || _lastGetCpuUsageTime == new DateTime())
			{
				_lastGetCpuUsageTime = DateTime.Now;
				_lastTotalProcessorTime = p.TotalProcessorTime;
			}
			else
			{
				DateTime currentTime = DateTime.Now;
				TimeSpan currentTotalProcessorTime = p.TotalProcessorTime;

				double cpuMillisDelta = currentTotalProcessorTime.TotalMilliseconds -
				                        _lastTotalProcessorTime.TotalMilliseconds;
				double totalMillisDelta =
					currentTime.Subtract(_lastGetCpuUsageTime.Value).TotalMilliseconds;

				double cpuUsage = cpuMillisDelta / totalMillisDelta /
				                  Environment.ProcessorCount;

				_lastGetCpuUsageTime = currentTime;
				_lastTotalProcessorTime = currentTotalProcessorTime;

				return cpuUsage;
			}

			return -1;
		}

		public void Reset()
		{
			ReportStart = DateTime.Now;
		}

		public ServiceLoad Clone()
		{
			return new ServiceLoad(ProcessCapacity)
			{
				CurrentProcessCount = CurrentProcessCount,
				ServerUtilization = ServerUtilization,
				ReportStart = ReportStart
			};
		}

		public override string ToString()
		{
			return
				$"{CurrentProcessCount} of {ProcessCapacity} ongoing requests, " +
				$"server utilization: {ServerUtilization}";
		}
	}
}
