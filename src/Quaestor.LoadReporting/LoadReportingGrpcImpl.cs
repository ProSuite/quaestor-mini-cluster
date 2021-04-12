using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using JetBrains.Annotations;

namespace Quaestor.LoadReporting
{
	public class LoadReportingGrpcImpl : LoadReportingGrpc.LoadReportingGrpcBase
	{
		private readonly Dictionary<string, IServiceLoad> _loadByService =
			new Dictionary<string, IServiceLoad>();

		public void AllowMonitoring([NotNull] string serviceName,
		                            [NotNull] IServiceLoad serviceLoad)
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
				if (!_loadByService.TryGetValue(request.ServiceName, out IServiceLoad currentLoad))
				{
					// Unknown service;
					return Task.FromException<LoadReportResponse>(
						new RpcException(new Status(StatusCode.OutOfRange,
							$"Service name {request.ServiceName} not found.")));
				}

				if (currentLoad == null)
				{
					// Unknown load or unknown service;
					return Task.FromException<LoadReportResponse>(
						new RpcException(new Status(StatusCode.OutOfRange,
							$"Service {request.ServiceName} has no load.")));
				}

				serverStats.RequestCapacity = currentLoad.ProcessCapacity;
				serverStats.CurrentRequests = currentLoad.CurrentProcessCount;
				serverStats.CpuUsage = currentLoad.CpuUsage;

				clientStats.NumCallsFinished = currentLoad.ClientCallsFinished;
				clientStats.NumCallsStarted = currentLoad.ClientCallsStarted;
				clientStats.TimestampTicks = currentLoad.ReportStart.Ticks;

				currentLoad.ResetClientStats();
			}
			catch (Exception e)
			{
				var rpcException = new RpcException(
					new Status(StatusCode.Internal, e.ToString()), e.Message);

				return Task.FromException<LoadReportResponse>(rpcException);
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
