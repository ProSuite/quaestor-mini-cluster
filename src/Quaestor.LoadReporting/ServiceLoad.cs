using System;
using System.Diagnostics;
using System.Threading;

namespace Quaestor.LoadReporting
{
	/// <summary>
	///     Manages the current load of a service process used by the load balancing functionality.
	/// </summary>
	public class ServiceLoad : IServiceLoad
	{
		DateTime? _lastGetCpuUsageTime;
		private TimeSpan _lastTotalProcessorTime;
		private int _currentProcessCount;
		private double _knownLoadRate = -1;

		public ServiceLoad(int processCapacity = -1,
		                   int initialProcessCount = 0)
		{
			ProcessCapacity = processCapacity;

			ResetReportStart();
			ResetCurrentProcessCount(initialProcessCount);

			// Initialize last CPU time:
			GetCpuUsage();
		}

		/// <summary>
		///     The start time of the next load report.
		/// </summary>
		public DateTime ReportStart { get; private set; }

		/// <summary>
		///     The known load rate of the service. This method is thread-safe.
		/// </summary>
		public double KnownLoadRate
		{
			get => _knownLoadRate;
			set => Interlocked.Exchange(ref _knownLoadRate, value);
		}

		/// <summary>
		///     The capacity of the service.
		/// </summary>
		public int ProcessCapacity { get; set; }

		/// <summary>
		///     The number of currently ongoing processes.
		/// </summary>
		public int CurrentProcessCount => _currentProcessCount;

		public double ServerUtilization { get; set; }

		/// <summary>
		///     Increments <see cref="CurrentProcessCount" />. This method is thread-safe.
		/// </summary>
		public void StartRequest()
		{
			Interlocked.Increment(ref _currentProcessCount);
		}

		/// <summary>
		///     Decrements <see cref="CurrentProcessCount" />. This method is thread-safe.
		/// </summary>
		public void EndRequest()
		{
			Interlocked.Decrement(ref _currentProcessCount);
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

		/// <summary>
		///     Resets the <see cref="ReportStart" /> date and the <see cref="CurrentProcessCount" />.
		///     This method is thread-safe.
		/// </summary>
		/// <param name="initialProcessCount"></param>
		public void Reset(int initialProcessCount = 0)
		{
			ReportStart = DateTime.Now;

			_currentProcessCount = initialProcessCount;
		}

		/// <summary>
		///     Resets the <see cref="ReportStart" /> time to the current time.
		///     This method is thread-safe.
		/// </summary>
		public void ResetReportStart()
		{
			ReportStart = DateTime.Now;
		}

		/// <summary>
		///     Resets the <see cref="CurrentProcessCount" />.
		///     This method is thread-safe.
		/// </summary>
		/// <param name="count"></param>
		public void ResetCurrentProcessCount(int count = 0)
		{
			Interlocked.Exchange(ref _currentProcessCount, count);
		}

		public ServiceLoad Clone()
		{
			return new ServiceLoad(ProcessCapacity)
			{
				_currentProcessCount = CurrentProcessCount,
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
