using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;
using Quaestor.Microservices.Definitions;

namespace Quaestor.Microservices
{
	public class LoadReportingGrpcImpl : LoadReportingGrpc.LoadReportingGrpcBase
	{
		private readonly ILogger<LoadReportingGrpcImpl> _logger =
			Log.CreateLogger<LoadReportingGrpcImpl>();

		private readonly Dictionary<string, ServiceLoad> _loadByService =
			new Dictionary<string, ServiceLoad>();

		public void AllowMonitoring([NotNull] string serviceName,
		                            [NotNull] ServiceLoad serviceLoad)
		{
			_loadByService.Add(serviceName, serviceLoad);
		}

		public override Task<LoadReportResponse> ReportLoad(LoadReportRequest request,
		                                                    ServerCallContext context)
		{
			var serverStats = new ServerStats();
			var clientStats = new ClientStats();

			try
			{
				if (_loadByService.TryGetValue(request.ServiceName, out var currentLoad))
				{
					serverStats.RequestCapacity = currentLoad.ProcessCapacity;
					serverStats.CurrentRequests = currentLoad.CurrentProcessCount;
					serverStats.CpuUsage = currentLoad.CpuUsage;

					clientStats.NumCallsFinished = currentLoad.ClientCallsFinished;
					clientStats.NumCallsStarted = currentLoad.ClientCallsStarted;
					clientStats.TimestampTicks = currentLoad.ReportStart.Ticks;

					currentLoad.ResetClientStats();
				}
				else
				{
					// Unknown load or unknown service;
					serverStats.RequestCapacity = -1;
					serverStats.CurrentRequests = -1;
				}
			}
			catch (Exception e)
			{
				_logger.LogError(e, "Error reporting load.");

				return Task.FromException<LoadReportResponse>(e);
			}

			var result = new LoadReportResponse
			{
				ServerStats = serverStats,
				ClientStats = clientStats
			};

			return Task.FromResult(result);
		}
	}
}
