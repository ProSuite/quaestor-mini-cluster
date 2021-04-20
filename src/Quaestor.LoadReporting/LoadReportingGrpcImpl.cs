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
			var result = new LoadReportResponse();
			var serverStats = new ServerStats();

			result.ServerStats = serverStats;

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

				result.TimestampTicks = currentLoad.ReportStart.Ticks;
				result.KnownLoadRate = currentLoad.KnownLoadRate;

				serverStats.RequestCapacity = currentLoad.ProcessCapacity;
				serverStats.CurrentRequests = currentLoad.CurrentProcessCount;
				serverStats.ServerUtilization = currentLoad.ServerUtilization;

				currentLoad.Reset();
			}
			catch (Exception e)
			{
				var rpcException = new RpcException(
					new Status(StatusCode.Internal, e.ToString()), e.Message);

				return Task.FromException<LoadReportResponse>(rpcException);
			}

			return Task.FromResult(result);
		}
	}
}
