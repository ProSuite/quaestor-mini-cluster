using System;
using System.Diagnostics;
using JetBrains.Annotations;

namespace Quaestor.Microservices
{
	public class ServiceLoad
	{
		DateTime? _lastGetCpuUsageTime;
		private TimeSpan _lastTotalProcessorTime;

		public ServiceLoad(int processCapacity = -1)
		{
			ProcessCapacity = processCapacity;

			ResetClientStats();

			// Initialize last CPU time:
			GetCpuUsage();
		}

		public int ProcessCapacity { get; set; }
		public int CurrentProcessCount { get; set; }
		public double CpuUsage { get; set; }

		public int ClientCallsStarted { get; set; }
		public int ClientCallsFinished { get; set; }
		public DateTime ReportStart { get; private set; }

		[PublicAPI]
		public void StartRequest()
		{
			CurrentProcessCount++;
			ClientCallsStarted++;
		}

		[PublicAPI]
		public void EndRequest()
		{
			CurrentProcessCount--;
			ClientCallsFinished++;
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
				                  System.Environment.ProcessorCount;

				_lastGetCpuUsageTime = currentTime;
				_lastTotalProcessorTime = currentTotalProcessorTime;

				return cpuUsage;
			}

			return -1;
		}

		public void ResetClientStats()
		{
			ReportStart = DateTime.Now;
			ClientCallsStarted = 0;
			ClientCallsFinished = 0;
		}

		public ServiceLoad Clone()
		{
			return new ServiceLoad(ProcessCapacity)
			{
				CurrentProcessCount = CurrentProcessCount,
				CpuUsage = CpuUsage,
				ClientCallsStarted = ClientCallsStarted,
				ClientCallsFinished = ClientCallsFinished,
				ReportStart = ReportStart
			};
		}

		public override string ToString()
		{
			return
				$"{CurrentProcessCount} of {ProcessCapacity} ongoing requests, " +
				$"{ClientCallsStarted} client requests started, {ClientCallsFinished} finished. " +
				$"CPU: {CpuUsage}";
		}
	}
}
